using System;
using System.Collections.Generic;
using UnityEngine;

namespace WindPower.Adapters;

/// <summary>Hands each frame to the table check. Each distinct failure is logged once and never rethrown into the game.</summary>
public sealed class WindTicker : MonoBehaviour
{
    private const int MaxReportedFailures = 10;

    internal static Action<float> Check = _ => { };
    internal static Action<string> Log = _ => { };
    private static readonly HashSet<string> ReportedFailures = new();

    public WindTicker(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        try
        {
            Check(Time.realtimeSinceStartup);
        }
        catch (Exception ex)
        {
            if (ReportedFailures.Count < MaxReportedFailures && ReportedFailures.Add(ex.GetType().FullName + ": " + ex.Message)) Log("update failed (each distinct failure reported once): " + ex);
        }
    }
}
