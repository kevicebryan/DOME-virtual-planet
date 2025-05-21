using System;
using System.Collections.Generic;
using UnityEngine;

public class DomeCommand
{
    private static readonly Dictionary<string, Action<List<string>>> Functions = new();

    public static void Register(string name, Action<List<string>> function)
    {
        Functions[name.ToLower()] = function;
    }
    
    public static void Invoke(string command, params string[] args)
    {
        Get(command).Invoke(new List<string>(args));
    }

    public static Action<List<string>> Get(string name)
    {
        return Functions[name.ToLower()];
    }

    public string Name;
    public List<string> Arguments;

    public DomeCommand(string name)
    {
        Name = name;
        Arguments = new List<string>();
    }

    public void Invoke()
    {
        Debug.Log("name: " + Name);
        Arguments.ForEach(arg => Debug.Log("arg: " + arg));
        if (Functions.TryGetValue(Name.ToLower(), out var function))
        {
            function(Arguments);
        }
        else
        {
            Debug.LogError($"Function '{Name}' not found.");
        }
    }
}