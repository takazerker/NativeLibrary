using UnityEngine;
using UnityEditor;

[NativeLibraryPath("Assets/Editor/ExampleDLL.dll")]
public class ExampleDLL : NativeLibrary
{
    public delegate int TestFunctionDelegate();

    [NativeFunction]
    public static TestFunctionDelegate TestFunction;

    [MenuItem("File/Invoke TestFunction")]
    static void InvokeTestFunction()
    {
        Debug.Log(TestFunction());
    }
}
