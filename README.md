# NativeLibrary

--------------------------------------

Unity(主にエディタ)でネイティブのDLLをホットリロード可能にするユーティリティクラスです。  
とにかくコンパクトな物になるように作りました。Windowsのみ対応しています。

### 仕組み
TempフォルダにDLLをコピーし、LoadLibraryを使って動的にDLLを読み込んでいます。

### 使い方
1. NativeLibraryから派生させたクラスを作成し、NativeLibraryPath属性でDLLのパスを指定します。
2. クラスメンバにNativeFunction属性を付けたDLL内の関数と同名のスタティックなデリゲートを定義します。
3. 完成。後はアセンブリロードのタイミングでデリゲートが自動生成されます。

```
ExampleDLL.cs

[NativeLibraryPath("Assets/Editor/ExampleDLL.dll")]
public class ExampleDLL : NativeLibrary
{
    public delegate int TestFunctionDelegate();

    [NativeFunction]
    public static TestFunctionDelegate TestFunction;
}
```

