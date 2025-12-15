using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class MousePos : MonoBehaviour
{
    //public static MousePos Minst;
    public Vector3 mousePos;
    PhotonView photonView;
    // Start is called before the first frame update
    private void Awake()
    {
        //Minst = this; 
    }
 
    void Start()
    {
        photonView = GetComponent<PhotonView>();
    }

    // Update is called once per frame
    void Update()
    {

        if (photonView.IsMine == false) return;
        mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        this.transform.position = mousePos;
    }
}
