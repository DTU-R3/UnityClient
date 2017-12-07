﻿using Messages.std_msgs;
using Ros_CSharp;
using UnityEngine;

/// <summary>
/// Ultrasound agent that sends or receives ultrasound data
/// </summary>
public class ROSTransformHeading : ROSAgent
{
    private const string TOPIC = "/robot_heading";

    private NodeHandle _nodeHandle;
    private Subscriber<Float32> _subscriber;
    private Publisher<Float32> _publisher;
    private bool _isRunning;
    private AgentJob _job;
    
    ///<summary>
    ///Starts advertising loop
    /// <param name="job">Defines behaviour of agent</param>
    /// <param name="rosNamespace">Namespace the agent listens or writes to + topic</param>
    ///</summary>
    public override void StartAgent(AgentJob job, string rosNamespace) {
        base.StartAgent(job, rosNamespace);
        if (_isRunning) return;
        _nodeHandle = new NodeHandle();
        if(job == AgentJob.Subscriber)
            _subscriber = _nodeHandle.subscribe<Float32>(TOPIC, 1, ReceivedData);
        else if (job == AgentJob.Publisher)
            _publisher = _nodeHandle.advertise<Float32>(TOPIC, 1, false);
        _isRunning = true;
        _job = job;
        Application.logMessageReceived += LogMessage;
    }

    public override void PublishData(object data)
    {
        if (_job != AgentJob.Publisher) return;
        Float32 dataString = (Float32) data;
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