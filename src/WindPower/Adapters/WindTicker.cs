using System;
using System.Collections.Generic;
using ExiledToolkit.Kit;
using UnityEngine;

namespace WindPower.Adapters;

/// <summary>Hands each frame to the table check. Each distinct failure is logged once and never rethrown into the game.</summary>
public sealed class WindTicker : MonoBehaviour
{
    private const int MaxReportedFailures = 10;

    internal static Action<float> Check = _ => { };
    internal static Action<string> Log = _ => { };
    private static readonly HashSet<string> ReportedFailures = new();
    private static readonly double LoadedAt = Clock.Now;

    /// <summary>
    /// Real seconds since the mod loaded, from the Toolkit clock. Counted from load so a float keeps its precision
    /// however long the computer has been on.
    /// </summary>
    internal static float Now => (float)(Clock.Now - LoadedAt);

    public WindTicker(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        try
        {
            Check(Now);
        }
        catch (Exception ex)
        {
            if (ReportedFailures.Count < MaxReportedFailures && ReportedFailures.Add(ex.GetType().FullName + ": " + ex.Message)) Log("update failed (each distinct failure reported once): " + ex);
        }
    }
}
