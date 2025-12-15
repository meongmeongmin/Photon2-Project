using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class Foot : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] Vector3 forceVec;
    [SerializeField] Transform mousePos;
    //[SerializeField] Vector2 maxVelocity;
    //[SerializeField] Vector2 curVelocity;
    //[SerializeField] float power;
    [SerializeField] float disLimit;
    Rigidbody2D rigid;
    MousePos getMousePos;
    [SerializeField] Transform center;
    private void Awake()
    {
        rigid = this.GetComponent<Rigidbody2D>();
    }
    void Start()
    {
        //getMousePos = MousePos.Minst;
    }

    // Update is called once per frame
    void Update()
    {
        this.mousePos.transform.position = getMousePos.mousePos;

        float dis = Vector2.Distance(center.position, mousePos.position);
        if(dis < disLimit)
        {
            this.rigid.position = mousePos.transform.position;
        }
        
        //limitVelocity();
        //curVelocity = this.GetComponent<Rigidbody2D>().velocity;

        //forceVec = this.mousePos.transform.position - this.transform.position;

        //float a = Mathf.Atan2(mousePos.position.y - this.transform.position.y, mousePos.position.x - this.transform.position.x) * Mathf.Rad2Deg;
        //Debug.Log(forceVec.normalized);
    }
    private void FixedUpdate()
    {
        //this.rigid.position = mousePos.transform.position;
        //this.rigid.AddForce(forceVec * power, ForceMode2D.Force);
    }
    //void limitVelocity()
    //{
    //    if(rigid.velocity.x > maxVelocity.x)
    //    {
    //        rigid.velocity = new Vector2(maxVelocity.x, rigid.velocity.y);
    //    }
    //    if (rigid.velocity.x < maxVelocity.x * -1)
    //    {
    //        rigid.velocity = new Vector2(maxVelocity.x * -1, rigid.velocity.y);
    //    }
    //    if (rigid.velocity.y > maxVelocity.y)
    //    {
    //        rigid.velocity = new Vector2(rigid.velocity.x,maxVelocity.y);
    //    }
    //    if (rigid.velocity.y < maxVelocity.y * -1)
    //    {
    //        rigid.velocity = new Vector2(rigid.velocity.x, maxVelocity.y * -1);
    //    }
    //}
}
