using System;
using UnityEngine;

//Connects to the Theta S UVC stiching software and outputs a square image
public class ThetaWebcamStream : MonoBehaviour
{
    [SerializeField] private MeshRenderer _primarySphere;
    [SerializeField] private MeshRenderer _icoSphere;
    [SerializeField] private bool _useDummyImage;
    [SerializeField] private Texture _dummyImage;
    [SerializeField] private Texture _text;
    [SerializeField] private AudioClip defaUltTestClip;

    private bool microphoneEnabled = false;
    private AudioSource audiosource;


    public void Awake()
    {
        audiosource = gameObject.GetComponent<AudioSource>();
    }

    public void StartStream()
    {
        if (_useDummyImage)
        {
            _primarySphere.material.mainTexture = _dummyImage;
            return;
        }

       
        
       WebCamTexture mycam = new WebCamTexture();
        //I think this name is wrong here. Only when RICOH THETA S is part of the devices list the camera actually outputs information
        string camName = "THETA UVC FullHD Blender";
        mycam.deviceName = camName;
        _primarySphere.sharedMaterial.mainTexture = mycam;
        _text = mycam;
        mycam.Play();

        //Audio stream from microphone

        
        /*if (audiosource)
        {
            Debug.Log("Found Audiosource");

            //Create soundclip from microphone, by making a small recording of  1 second duration.
            //Name for the microphone is either manually the one below or : Microphone.Devices[0] . However this seems a safer choice in the case that there is another microphone source in that slot.
            AudioClip micClip = Microphone.Start("Microphone (RICOH THETA S)", true, 1, 44100);
            audiosource.clip = micClip ;
            audiosource.loop = true;
            audiosource.Play();
            microphoneEnabled = true;
        }
        else
        {
            Debug.Log("No audiosource attached to gameobject");
        }
        */

        //Debug show microphone devices
        foreach (var mic in Microphone.devices)
        {
            Debug.Log(mic);
        }

        //Debug show the devices to ensure that theta camera is connected
        //
        foreach (var variable in WebCamTexture.devices)
        {
            Debug.Log(variable.name);
        }
    }

    public void Update()
    {

        //mute/unmute microphone  microphone with hotkey S
        if (Input.GetKeyDown("s"))
        {
            audiosource.mute = microphoneEnabled;
            microphoneEnabled = !microphoneEnabled;
        }


    }

}