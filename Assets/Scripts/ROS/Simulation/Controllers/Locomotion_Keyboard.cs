﻿using Ros_CSharp;
using UnityEngine;
using String = Messages.std_msgs.String;

/// <summary>
/// Ultrasound agent that sends or receives ultrasound data
/// </summary>
public class Locomotion_Keyboard : ROSAgent {
    private const string TOPIC = "/control/locomotion";

    private NodeHandle _nodeHandle;
    private Subscriber<String> _subscriber;
    private Publisher<String> _publisher;
    private bool _isRunning;
    private AgentJob _job;

    ///<summary>
    ///Starts advertising loop
    /// <param name="job">Defines behaviour of agent</param>
    /// <param name="rosNamespace">Namespace the agent listens or writes to + topic</param>
    ///</summary>
    public override void StartAgent(AgentJob job) 
    {
        base.StartAgent(job);
        if (_isRunning) return;
        _nodeHandle = new NodeHandle();
        if (job == AgentJob.Subscriber)
            _subscriber = _nodeHandle.subscribe<String>(TOPIC, 1, ReceivedData);
        else if (job == AgentJob.Publisher)
            _publisher = _nodeHandle.advertise<String>(TOPIC, 1, false);
        _isRunning = true;
        _job = job;
        //Application.logMessageReceived += LogMessage;
    }

    public override void PublishData(object data) {
        if (_job != AgentJob.Publisher) return;
        String dataString = (String)data;
        _publisher.publish(dataString);
    }

    ///<summary>
    ///Stops advertising loop
    ///</summary>
    public void Stop() {
        if (!_isRunning) return;
        _nodeHandle.shutdown();
        _subscriber = null;
        _nodeHandle = null;
    }

}