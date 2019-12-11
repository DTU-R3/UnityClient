using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class TimeLogger {

    public static TimeLogger Instance
    {
        get { return _instance ?? (_instance = new TimeLogger()); }
    }
    private static TimeLogger _instance;

    public enum TimeLogType
    {
        Virtual,
        QTest
    }

    private bool _isRunning;
    private DateTime _starTime, _endTime;
    private const string PATH = "Assets/StreamingAssets/TestLogData/{0}-TimeLogData.txt";
    private TimeLogType _logType;

    private TimeLogger() { }

    public void Start(TimeLogType type)
    {
        if (!_isRunning)
        {
            _logType = type;
            _starTime = DateTime.Now;
            _isRunning = true;
        }
    }

    public void Stop()
    {
        _endTime = DateTime.Now;
        SaveToLog();
    }

    private void SaveToLog()
    {

        //Write some text to the test.txt file
        StreamWriter writer = new StreamWriter(string.Format(PATH, _logType), true);
        writer.WriteLine("{");
        writer.WriteLine("\"Start Time\": \"" + _starTime + "\"");
        writer.WriteLine("\"End Time\": \"" + _endTime + "\"");
        writer.WriteLine("},");
        writer.Close();

        //Re-import the file to update the reference in the editor
        AssetDatabase.ImportAsset(string.Format(PATH, _logType));
    }
}
