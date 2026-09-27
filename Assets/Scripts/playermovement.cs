using UnityEngine;

public class playermovement : MonoBehaviour
{
    public Animator anime;//new code for anime
    public CharacterController2D controller;//new codes
    public float runSpeed = 40f;
    public float horizontalMove;
    public bool jump;//new code jump
    void Update()
    {
        horizontalMove = Input.GetAxis("Horizontal") * runSpeed;
        anime.SetFloat("Speed",Mathf.Abs(horizontalMove));
        if (Input.GetButtonDown("Jump"))
        {
            jump = true;
            anime.SetBool("jump",true);//anime jump
        }}
    private void FixedUpdate(){
        controller.Move(horizontalMove * Time.fixedDeltaTime, false,jump);//new code
    jump=false;
    anime.SetBool("jump",false);
    }
}
