#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ネイティブライブラリのベースクラス
public abstract class NativeLibrary
{
}

// ネイティブライブラリの属性
//
// - NativeLibraryクラスに付けて使用します
//
// 例)
//  [NativeLibrary("Assets/Test.DLL")] 
//  class SomeNativeLibrary: NativeLibrary
//  {
//   ...
//  }
//
[System.AttributeUsage(System.AttributeTargets.Class)]
public class NativeLibraryPathAttribute : System.Attribute
{
    public string DLLPath;

    public NativeLibraryPathAttribute(string filename)
    {
        DLLPath = filename;
    }
}

// ネイティブライブラリ内の関数を定義する属性
//
// - NativeLibrary派生クラス内が持つ静的デリゲートフィールドに付けると
//   ライブラリ読み込み時にフィールドと同名の関数が検索され、
//   見つかった関数を呼び出すデリゲートが自動で生成されます。
//
// 例)
//  public delegate int TestFunctionDelegate();
// 
//  [NativeFunction]
//  public static TestFunctionDelegate TestFunction;
//
[System.AttributeUsage(System.AttributeTargets.Field)]
public class NativeFunction : System.Attribute
{
}

#endif // UNITY_EDITOR
