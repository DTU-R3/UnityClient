using UnityEngine;

//Connects to the Theta S UVC stiching software and outputs a square image
public class ThetaWebcamStream : MonoBehaviour
{
    [SerializeField] private MeshRenderer _primarySphere;
    [SerializeField] private MeshRenderer _icoSphere;
    [SerializeField] private bool _useDummyImage;
    [SerializeField] private Texture _dummyImage;
    [SerializeField] private Texture _text;

    public void StartStream()
    {
        if (_useDummyImage)
        {
            _primarySphere.material.mainTexture = _dummyImage;
            return;
        }

       
        
       WebCamTexture mycam = new WebCamTexture();
        string camName = "THETA UVC FullHD Blender";
        mycam.deviceName = camName;
        _primarySphere.sharedMaterial.mainTexture = mycam;
        _text = mycam;
        if (!mycam.isPlaying)
        {
            Debug.Log("Playing cam");
            mycam.Play();
        }
        foreach (var variable in WebCamTexture.devices)
        {
         
            Debug.Log(variable.name);
        }
    }
}