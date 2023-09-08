using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class WebSocketMessageEvennia
{
    abstract public string GetCmd();
    abstract public void SetCmd(string cmd);


    abstract public List<string> GetArgs();
    abstract public void SetArgs(List<string> args);


    // Get a typed kwarg (must know it's type)
    abstract public T GetKwarg<T>(string key);

    abstract public void SetKwarg<T>(string key, T value);

    abstract public bool HasKwarg(string key);

    abstract public int GetNumKwarg();
}
