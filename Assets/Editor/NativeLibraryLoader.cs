#if UNITY_EDITOR_WIN
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ネイティブDLLの読み込みを管理するクラス
public class NativeLibraryLoader : ScriptableObject
{

    [DllImport("kernel32", SetLastError=true)]
    static extern System.IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32", SetLastError = true)]
    static extern System.IntPtr GetProcAddress(System.IntPtr hModule, string lpProcName);

    [DllImport("kernel32", SetLastError = true)]
    static extern bool FreeLibrary(System.IntPtr hModule);

    // 読み込んだライブラリの情報
    [System.Serializable]
    class Library
    {
        public string ClassName;
        public string LibraryPath;
        public string CopyPath;
        public System.Type ClassType;
        public System.IntPtr Module;

        [System.NonSerialized]
        public volatile bool HotReload;

        FileSystemWatcher mFileWatcher;
        List<FieldInfo> mDelegateFields = new List<FieldInfo>();

        // 読み込み
        public void Reload()
        {
            HotReload = false;

            Unload();

            // ファイルの存在を確認
            if (!File.Exists(LibraryPath))
            {
                Debug.LogErrorFormat("Library not found: {0}", LibraryPath);
                return;
            }

            // コピー名を決める
            if (string.IsNullOrEmpty(CopyPath))
            {
                string libFileName = Path.GetFileName(LibraryPath);

                do
                {
                    System.Guid guid = System.Guid.NewGuid();
                    CopyPath = "Temp/" + guid.ToString() + "-" + libFileName;
                }
                while (File.Exists(CopyPath));
            }

            // ファイルのコピー
            File.Copy(LibraryPath, CopyPath, true);

            // コピー先から読み込み
            Module = LoadLibrary(CopyPath);
            if (Module == System.IntPtr.Zero)
            {
                Debug.LogErrorFormat("Failed to load library: {0} ErrorCode: {1:X8}", LibraryPath, Marshal.GetLastWin32Error());
                return;
            }

            LinkFunctions();

            // ファイルの監視
            mFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(LibraryPath), Path.GetFileName(LibraryPath));
            mFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
            mFileWatcher.Changed += OnLibraryFileChange;
            mFileWatcher.EnableRaisingEvents = true;
        }

        // ファイルの書き換えイベント
        void OnLibraryFileChange(object sender, FileSystemEventArgs e)
        {
            // 別スレッドから呼ばれるのでフラグを立ててメインスレッドにリロードさせる
            HotReload = true;
        }

        // 関数のリンク
        void LinkFunctions()
        {
            FieldInfo[] fields = ClassType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < fields.Length; ++i)
            {
                if (fields[i].FieldType.IsSubclassOf(typeof(System.Delegate)) && NativeFunction.IsDefined(fields[i], typeof(NativeFunction)))
                {
                    LinkFunction(fields[i]);
                }
            }
        }

        // 関数のリンク
        void LinkFunction(FieldInfo field)
        {
            mDelegateFields.Add(field);

            System.IntPtr procAddr = GetProcAddress(Module, field.Name);

            if (procAddr == System.IntPtr.Zero)
            {
                Debug.LogErrorFormat("Function '{0}' not found in {1}", field.Name, LibraryPath);
                return;
            }

            System.Delegate del = Marshal.GetDelegateForFunctionPointer(procAddr, field.FieldType);
            field.SetValue(null, del);
        }

        // 開放
        public void Unload()
        {
            // ファイルの監視を中止
            if (mFileWatcher != null)
            {
                mFileWatcher.EnableRaisingEvents = false;
                mFileWatcher.Dispose();
                mFileWatcher = null;
            }

            // デリゲートのフィールドを全てリセット
            for (int i = 0; i < mDelegateFields.Count; ++i)
            {
                mDelegateFields[i].SetValue(null, null);
            }
            mDelegateFields.Clear();

            // ライブラリ開放
            if (Module != System.IntPtr.Zero)
            {
                FreeLibrary(Module);
                Module = System.IntPtr.Zero;
            }
        }
    }

    // シングルトンの参照
    static NativeLibraryLoader mInstance;

    // 読み込まれたライブラリのリスト
    List<Library> mLibraries = new List<Library>();

    // シングルトン
    static NativeLibraryLoader GetInstance()
    {
        if (mInstance == null)
        {
            var objs = Resources.FindObjectsOfTypeAll<NativeLibraryLoader>();

            if (objs.Length > 0)
            {
                mInstance = objs[0];
            }
            else
            {
                mInstance = CreateInstance<NativeLibraryLoader>();
                mInstance.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        return mInstance;
    }

    // アセンブリロード後の初期化
    [InitializeOnLoadMethod]
    static void OnLoad()
    {
        EditorApplication.update += GetInstance().OnUpdate;
    }

    // 更新処理
    void OnUpdate()
    {
        for (int i = 0; i < mLibraries.Count; ++i)
        {
            if (mLibraries[i].HotReload)
            {
                Debug.LogFormat("Hot reloading library: {0}", mLibraries[i].LibraryPath);
                mLibraries[i].Reload();
            }
        }
    }

    // ライブラリの読み込み
    void LoadLibraries()
    {
        foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass || type.IsAbstract || !type.IsSubclassOf(typeof(NativeLibrary)))
                {
                    continue;
                }

                NativeLibraryPathAttribute[] libraryAttr = type.GetCustomAttributes(typeof(NativeLibraryPathAttribute), false) as NativeLibraryPathAttribute[];

                if (libraryAttr.Length <= 0)
                {
                    continue;
                }

                LoadLibrary(type, libraryAttr[0]);
            }
        }
    }

    // ライブラリの読み込み
    void LoadLibrary(System.Type libClass, NativeLibraryPathAttribute libraryAttr)
    {
        string className = libClass.FullName;
        
        // 登録済みのライブラリを探す
        for (int i = 0; i < mLibraries.Count; ++i)
        {
            if (mLibraries[i].ClassName == className && mLibraries[i].LibraryPath == libraryAttr.DLLPath)
            {
                mLibraries[i].ClassType = libClass;
                mLibraries[i].Reload();
                return;
            }
        }

        // ライブラリはまだ登録されていないので新しく登録する
        Library newLib = new Library();
        newLib.ClassType = libClass;
        newLib.ClassName = className;
        newLib.LibraryPath = libraryAttr.DLLPath;
        mLibraries.Add(newLib);

        newLib.Reload();
    }

    // 読み込まれたライブラリを全て開放
    void UnloadLibraries()
    {
        for (int i = 0; i < mLibraries.Count; ++i)
        {
            mLibraries[i].Unload();
        }
    }

    private void OnEnable()
    {
        LoadLibraries();
    }
    
    private void OnDisable()
    {
        UnloadLibraries();
    }
    
}

#endif // UNITY_EDITOR_WIN
