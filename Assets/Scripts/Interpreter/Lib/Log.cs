using Interpreter.Eval.Type;
using UnityEngine;

namespace Interpreter.Lib
{
    // [UserFunction("console_log")]
    public class Log
    {
        public void Exec(String msg)
        {
            Debug.Log($"user msg: {msg}");
        }
    }
}