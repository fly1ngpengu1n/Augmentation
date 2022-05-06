using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Shooter : Player
{
    enum ANIMATION_STATE
    {
        IDLE,
        RUN,
        JUMP,
        ATTACK,
        DEATH,
    }

    public string[] animationStr;
    public float grabSpeed = 10;
    public float grapplingRange = 10;
    public GameObject arm;
    public LayerMask grapplingMask;
    public GameObject bullet;
    public float attackSpeed = 0.5f;
    private float attackTimer = 0;

    private LineRenderer grapplingAim;
    private bool isGrappling = false;
    private GameObject grabbedBlock;
    private Coroutine resetAnim = null;

    private bool isAiming = false;
    private Vector2 aimJoyStick;
    private float currentArmsAngle = 0;

    public override void Start()
    {
        base.Start();
        this.transform.rotation = Quaternion.Euler(0, -90, 0);
        this.grapplingAim = this.GetComponent<LineRenderer>();

        anim.clip = this.currentAnim = anim.GetClip(animationStr[(int)ANIMATION_STATE.IDLE]);
        anim.Play();
    }
    public override void Update()
    {
        if (this.isDead)
            return;

        if (this.isAttacking && this.isAiming)
            this.Shoot();

        if (this.grabbedBlock != null)
        {
            Vector3 newPos = Vector3.MoveTowards(this.transform.position, this.grabbedBlock.transform.position, this.grabSpeed * Time.deltaTime);
            this.transform.position = newPos;

            this.m_body.isKinematic = true;
        }

        if (this.orientation != this.joystickSide && !this.isAiming && this.joystickSide != 0)
        {
            this.orientation = this.joystickSide;
            this.rotationAnimeCoro = StartCoroutine(this.RotateAnimation());
        }

        this.RotateArmsUpdate();
        this.UpdateGravity();
        this.UpdateAnimationState();
    }
    protected override void UpdateAnimationState()
    {
        if (this.joystickSide != 0)
        {
            if (this.currentAnim == this.anim.GetClip(this.animationStr[(int)ANIMATION_STATE.RUN]) || this.isAttacking || !this.canJump || this.isAiming)
                return;

            this.anim.clip = this.currentAnim = this.anim.GetClip(this.animationStr[(int)ANIMATION_STATE.RUN]);
            this.anim.Play();

            //this.footstep.Play();
        }
        else if (Mathf.Abs(this.m_body.velocity.x) < 0.001f)
        {
            if (this.currentAnim == this.anim.GetClip(this.animationStr[(int)ANIMATION_STATE.IDLE]) || this.isAttacking || !this.canJump || this.isAiming)
                return;

            this.anim.clip = this.currentAnim = this.anim.GetClip(this.animationStr[(int)ANIMATION_STATE.IDLE]);
            this.anim.Play();

            //this.footstep.Stop();
        }
    }
    public void Jump(InputAction.CallbackContext context)
    {
        if (this.isDead || !context.started || !this.canJump)
            return;

        this.canJump = false;
        this.inTheAir = true;

        // this.src.PlayOneShot(this.clip[3]);
        // this.footstep.Stop();

        this.m_body.velocity = new Vector3(this.m_body.velocity.x, 0, 0);
        this.m_body.AddForce(new Vector3(0, this.jumpForce, 0), ForceMode.Impulse);

        if (!this.isAttacking && !this.isGrappling && !this.isAiming)
        {
            this.anim.clip = this.currentAnim = this.anim.GetClip(this.animationStr[(int)ANIMATION_STATE.JUMP]);
            this.anim.Play();
        }

        //this.m_audio.PlayOneShot(this.clip[3]);
        //this.footstep.Stop();
    }
    public override void OnCollisionEnter(Collision collision)
    {
        if (this.isDead)
            return;

        if (collision.gameObject.CompareTag("AI") && collision.GetContact(0).normal == Vector3.up)
        {
            Vector3 jump = new Vector3(-this.transform.right.x * this.orientation * this.bunnyHopForce, this.jumpForce, 0);
            this.m_body.velocity = new Vector3(this.m_body.velocity.x, 0, 0);
            this.m_body.AddForce(jump, ForceMode.Impulse);
            this.canJump = true;

            // this.src.PlayOneShot(this.clip[3]);
            // this.footstep.Stop();

            if (!this.isGrappling && !this.isAttacking)
            {
                this.anim.clip = this.currentAnim = this.anim.GetClip(this.animationStr[(int)ANIMATION_STATE.JUMP]);
                this.anim.Play();
            }
        }
        else if (collision.GetContact(0).normal == Vector3.up)
        {
            this.canJump = true;
            this.inTheAir = false;
            // this.src.PlayOneShot(this.clip[2]);
            // this.footstep.Stop();
        }
    }
    public override IEnumerator Dead()
    {
        this.isDead = true;
        this.m_body.velocity = Vector3.zero;

        this.anim.clip = this.currentAnim = this.anim.GetClip(animationStr[(int)ANIMATION_STATE.DEATH]);
        this.anim.Play();

        //this.m_audio.PlayOneShot(this.clip[5]);
        //this.footstep.Stop();

        yield return new WaitForSeconds(this.anim.GetClip(animationStr[(int)ANIMATION_STATE.DEATH]).length);
        Destroy(this.gameObject);
        this.FireDeath();
    }
    public void RotateArm(InputAction.CallbackContext context)
    {
        if (this.isDead)
            return;

        if (context.started)
        {
            this.isAiming = true;
            if (this.resetAnim != null)
                StopCoroutine(this.resetAnim);
            this.resetAnim = StartCoroutine(this.ResetAnimation());
        }
        else if (context.canceled)
        {
            this.isAiming = false;
            this.grapplingAim.SetPosition(0, this.arm.transform.position);
            this.grapplingAim.SetPosition(1, this.arm.transform.position);
            this.anim.Play();
        }

        if (context.control.device is Gamepad)
        {
            Vector2 joystick = context.ReadValue<Vector2>();
            this.aimJoyStick = Player.InputWithRadialDeadZone(Shooter.Remap(joystick.x, -1, 1, 0, Screen.width), Shooter.Remap(joystick.y, -1, 1, 0, Screen.height));
        }
        else
            this.aimJoyStick = context.ReadValue<Vector2>();
    }
    public static float Remap(float value, float from1, float to1, float from2, float to2)
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }
    private void RotateArmsUpdate()
    {
        if (this.isDead || this.rotationAnimeCoro != null || !this.isAiming)
            return;

        Vector3 screenPos = GameManager.Cam.WorldToScreenPoint(this.arm.transform.position);
        this.currentArmsAngle = 90 + Mathf.Atan2(this.aimJoyStick.y - screenPos.y, this.aimJoyStick.x - screenPos.x) * Mathf.Rad2Deg;

        this.arm.transform.rotation = Quaternion.Euler(currentArmsAngle, -90, 90);

        if (this.orientation == 1 && (this.currentArmsAngle < 0 || this.currentArmsAngle > 180 ))
        {
            this.orientation = -1;
            this.rotationAnimeCoro = StartCoroutine(this.RotateAnimation());
        }
        else if (this.orientation == -1 && (this.currentArmsAngle > 0 && this.currentArmsAngle < 180))
        {
            this.orientation = 1;
            this.rotationAnimeCoro = StartCoroutine(this.RotateAnimation());
        }

        //Show Calculate grap
        if (this.isGrappling)
        {
            Vector3 angle = new Vector3(Mathf.Cos((this.currentArmsAngle - 90) * Mathf.Deg2Rad), Mathf.Sin((this.currentArmsAngle - 90) * Mathf.Deg2Rad), 0);
            
            this.grapplingAim.SetPosition(0, this.arm.transform.position);
            this.grapplingAim.SetPosition(1, this.arm.transform.position + angle * this.grapplingRange);
        }
    }
    private IEnumerator ResetAnimation()
    {
        this.anim.clip = this.currentAnim = this.anim.GetClip(this.animationStr[(int)ANIMATION_STATE.IDLE]);
        this.anim.Rewind(this.animationStr[(int)ANIMATION_STATE.IDLE]);
        this.anim.Play();
        yield return new WaitForSeconds(0.001f);
        this.anim.Stop();
        this.resetAnim = null;
    }
    public override void Move(InputAction.CallbackContext context)
    {
        if (this.isDead)
            return;

        Vector2 direct = context.ReadValue<Vector2>();
        direct.x = Mathf.RoundToInt(direct.x);

        this.joystickSide = (int)direct.x;
        if (this.orientation != direct.x && direct.x != 0 && this.aimJoyStick == Vector2.zero)
        {
            this.orientation = (int)direct.x;
            if (this.rotationAnimeCoro == null)
                this.rotationAnimeCoro = StartCoroutine(this.RotateAnimation());
        }
    }
    public void Grappling(InputAction.CallbackContext context)
    {
        if (this.isDead)
            return;

        if (context.started)
        {
            this.isGrappling = true;
            this.grapplingAim.enabled = true;

            this.grapplingAim.SetPosition(0, this.transform.position);
            this.grapplingAim.SetPosition(1, this.transform.position);
        }
        else if (context.canceled)
        {
            this.isGrappling = false;
            this.grapplingAim.enabled = false;

            this.grapplingAim.SetPosition(0, this.transform.position);
            this.grapplingAim.SetPosition(1, this.transform.position);
            Vector3 angle = new Vector3(Mathf.Cos((this.currentArmsAngle - 90) * Mathf.Deg2Rad), Mathf.Sin((this.currentArmsAngle - 90) * Mathf.Deg2Rad), 0);

            RaycastHit hit;
            if (Physics.Raycast(this.arm.transform.position, angle, out hit, this.grapplingRange, this.grapplingMask))
                if (hit.collider.gameObject.CompareTag("GrabBlock"))
                {
                    this.grabbedBlock = hit.collider.gameObject;

                    //this.src.PlayOneShot(this.clip[0]);
                    //this.footstep.Stop();
                }
        }
    }
    public void OnTriggerEnter(Collider other)
    {
        if (this.isDead)
            return;

        if (other.gameObject.CompareTag("GrabBlock"))
        {
            this.grabbedBlock = null;
            this.m_body.isKinematic = false;
            this.canJump = true;
        }
    }
    public void RangeAttack(InputAction.CallbackContext context)
    {
        if (this.isDead)
            return;

        if (context.started)
        {
            this.isAttacking = true;
            if (Time.time - this.attackTimer < this.attackSpeed)
                this.attackTimer = Time.time;
        }
        else if (context.canceled)
            this.isAttacking = false;
    }
    public void Shoot()
    {
        //this.src.PlayOneShot(this.clip[1]);
        //this.footstep.Stop();
        if (Time.time - this.attackTimer > this.attackSpeed)
        {
            this.attackTimer = Time.time;
            Instantiate(this.bullet, this.arm.transform.position, Quaternion.Euler(0, 0, this.currentArmsAngle));
        }
    }
}