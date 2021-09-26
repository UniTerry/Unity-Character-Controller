using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [Header("Ground Detection")]
    public LayerMask whatIsGround; //what is considered as ground
    public Transform groundCheck; //position from where to check if the player is grounded

    [Header("Speed Settings")]
    public float acceleration = 300f; //player acceleration
    public float maxSpeed = 30f; //maximum speed
    public float slowDownFactor = 200f; //how much to slow down when there is no input
    public float jumpForce = 700f; //how high to jump
    public ParticleSystem speedParticle;

    [Header("Dash Settings")]
    public float dashDistance = 5f; //how far in a direction should the dash take the player
    public float dashForce = 500f; //how big of a speed boost should he get after the dash
    public float dashCooldown = 1.5f; //how long to wait until the player can dash again
    public ParticleSystem dashParticle;
    public Slider dashBar;

    [Header("Other")]
    public CameraMovement cameraParent; //the camera 
    public Rigidbody playerBody; //player's rigidbody
    public float slopeLimit = 45f; //maximum angle of a slope that can be climbed
    public float threshold = 5f; //the minimum velocity needed before changing the force applied from velocity change to acceleration
    public Vector3 crouchScale; //scale for crouching

    Vector3 playerScale; //scale for standing

    Vector3 direction; //direction of movement

    bool jump = false; //if the player can jump
    bool grounded = true; //if the player is on the ground
    bool onSlope = false; //if the player is on a slope
    bool crouched = false; //if the player is crouched or not
    bool secondJump = false; //if the player has double jumped
    bool canDoubleJump = false; //if the player can double jump
    bool canDash = true; //if the player can dash
    bool dash = false;

    float timeAtMaxSpeed = 0f; //time at 90% of the maximum speed
    float initialMaxSpeed = 50f; //the initial, unaltered speed limit

    float groundAcceleration = 300f; //amount of acceleration while grounded
    float airAcceleration = 150f; //amount of acceleration while airbourne
    float slopeAcceleration = 300f; //amount of acceleration while on a slope
    float crouchAcceleration = 100f; //amount of acceleration while crouching

    float playerHeight = 1f; //height of the caracter model

    //movement input
    float lateralInput = 0f;
    float forwardInput = 0f; 
    void Start()
    {
        dashBar.maxValue = dashCooldown;
        dashBar.value = dashCooldown;
        playerScale = transform.localScale;
        playerBody = GetComponent<Rigidbody>();
        playerHeight = transform.localScale.y;
        direction = Vector3.zero;
        initialMaxSpeed = maxSpeed;
        groundAcceleration = acceleration;
        airAcceleration = groundAcceleration / 2;
        crouchAcceleration = groundAcceleration / 3;
    }
    void Update()
    {
        GetInput();
    }
    void FixedUpdate()
    {
        Move();
    }
    void GetInput()
    {
        lateralInput = Input.GetAxis("Horizontal");
        forwardInput = Input.GetAxis("Vertical");
        direction = lateralInput * transform.right + forwardInput * transform.forward;
        
        CheckGround();

        if (Input.GetButtonDown("Jump")) {
            if (grounded)
                jump = true;
            else if (canDoubleJump)
                secondJump = true;
        }

        if (Input.GetKeyDown(KeyCode.LeftControl))
            Crouch();

        if (Input.GetKeyUp(KeyCode.LeftControl))
            Stand();

        if (Input.GetKeyDown(KeyCode.LeftShift))
            if (canDash)
                dash = true;

    }
    void Move()
    {
        //apply force according to the acceleration, multiplied by Time.fixedDeltaTime to be independent of framerate
        if (onSlope)
            playerBody.AddForce(direction * slopeAcceleration * Time.fixedDeltaTime, ForceMode.VelocityChange);
        else if (Mathf.Abs(playerBody.velocity.x) < threshold || Mathf.Abs(playerBody.velocity.z) < threshold)
            playerBody.AddForce(direction * acceleration * Time.fixedDeltaTime, ForceMode.VelocityChange);
        else
            playerBody.AddForce(direction * acceleration * Time.fixedDeltaTime, ForceMode.Acceleration);

        //cancel the input if the player is above the speed limit, page taken from Dani's book
        if (Mathf.Abs(playerBody.velocity.x) > maxSpeed)
            lateralInput = 0;

        if (Mathf.Abs(playerBody.velocity.z) > maxSpeed)
            forwardInput = 0;

        if (jump)
            Jump();

        if (secondJump)
            DoubleJump();

        if (dash)
            Dash();

        CounterMovement();
        ChangeSpeedLimit();
    }
    void Dash()
    {
        //if there is no input the dash should go forward, use project on plane in case the player dashes on a slope to not go through the ground
        if ((lateralInput == 0 && forwardInput == 0) || direction.normalized.magnitude == 0)
        {
            RaycastHit ground;
            Physics.Raycast(groundCheck.position, -transform.up, out ground, .5f);
            direction = Vector3.ProjectOnPlane(transform.forward, ground.normal);
        }

        //check if there is an obstacle in the way of the dash and if so move just the distance until the obstacle, otherwise move the whole dash distance
        //move the camera before the player body to avoid an out of body experience
        RaycastHit obstacle;

        if (playerBody.SweepTest(direction.normalized, out obstacle, dashDistance))
        {
            dashParticle.Play();
            cameraParent.transform.position += direction.normalized * obstacle.distance;
            transform.position += direction.normalized * obstacle.distance;
            canDash = false;
            StartCoroutine(DashReady());
        }
        else
        {
            //if the player is near a wall or something the sweep test wouldn't detect it and the dash would go through it, 
            //so don't dash if the overlap sphere detects more than 1 (the player) objects
            if (Physics.OverlapSphere(playerBody.position, .9f).Length == 1)
            {
                dashParticle.Play();
                playerBody.AddForce(direction.normalized * dashForce, ForceMode.VelocityChange);
                cameraParent.transform.position += direction.normalized * dashDistance;
                transform.position += direction.normalized * dashDistance;
                canDash = false;
                StartCoroutine(DashReady());
            }
        }
        dash = false;
    }
    IEnumerator DashReady()
    {
        float currentCooldown = 0f;
        while(currentCooldown < dashCooldown)
        {
            currentCooldown += Time.fixedDeltaTime;
            dashBar.value = currentCooldown;
            yield return null;
        }
        canDash = true;
    }
    void DoubleJump()
    {
        //stop the vertical velocity before jumping again so gravity doesn't cancel out the second jump
        playerBody.velocity = new Vector3(playerBody.velocity.x, 0, playerBody.velocity.z);
        playerBody.AddForce(transform.up * jumpForce * Time.fixedDeltaTime, ForceMode.Impulse);
        canDoubleJump = false;
        secondJump = false;
    }
    void Jump()
    {
        //apply a force upwards when the player wants to jump
        playerBody.AddForce(transform.up * jumpForce * Time.fixedDeltaTime, ForceMode.Impulse);
        jump = false;
    }
    void Stand()
    {
        //check if there is something above the player before standing up
        if (!Physics.CheckSphere(transform.position + transform.up * .5f, .5f, whatIsGround))
        {
            //switch from the crouching scale to the standing scale and adjust the position so the player doesn't clip through the floor
            crouched = false;
            transform.localScale = playerScale;
            transform.position = new Vector3(transform.position.x, transform.position.y + playerHeight / 2, transform.position.z);
        }
    }
    void Crouch()
    {
        //switch from the standing scale to the crouching scale and adjust the position so the player doesn't drop
        crouched = true;
        transform.localScale = crouchScale;
        transform.position = new Vector3(transform.position.x, transform.position.y - playerHeight / 2, transform.position.z);
    }
    void CounterMovement()
    {
        //get velocity relative to the player
        Vector3 localVelocity = transform.InverseTransformDirection(playerBody.velocity);

        float drag = acceleration * slowDownFactor;

        //when there is no input or the player's input is in the opposite direction slow down the player's velocity according to the slow down factor
        if ((lateralInput == 0) || (localVelocity.x > 0 && lateralInput < 0) || (localVelocity.x < 0 && lateralInput > 0))
            playerBody.AddForce(-localVelocity.x * transform.right * drag * Time.fixedDeltaTime, ForceMode.Acceleration);

        if ((forwardInput == 0) || (localVelocity.z > 0 && forwardInput < 0) || (localVelocity.z < 0 && forwardInput > 0))
            playerBody.AddForce(-localVelocity.z * transform.forward * drag * Time.fixedDeltaTime, ForceMode.Acceleration);
    }
    void ChangeSpeedLimit()
    {
        //if the player is above 90% of the speed limit for more than 5 seconds increase the maximum speed
        if (playerBody.velocity.magnitude > (maxSpeed * .8f))
        {
            if (speedParticle.isStopped)
                speedParticle.Play();

            timeAtMaxSpeed += Time.fixedDeltaTime;
            if (timeAtMaxSpeed > 5)
            {
                maxSpeed += maxSpeed * .1f;
                timeAtMaxSpeed = 0;
            }
        }
        else
        {
            if (speedParticle.isPlaying)
                speedParticle.Stop();

            timeAtMaxSpeed = 0;

            //if the player is not moving bring the speed limit back down
            if (Mathf.Round(playerBody.velocity.magnitude) == 0)
                maxSpeed = initialMaxSpeed;
        }
    }
    void CheckGround()
    {
        //check if the player is grounded with a checksphere, this doesn't return information about the surface the player is on so we need another raycast to check if the surface is sloped
        if (Physics.CheckSphere(groundCheck.position, .5f, whatIsGround))
        {
            if(crouched)
                acceleration = crouchAcceleration;
            else
                acceleration = groundAcceleration;

            canDoubleJump = true;
            grounded = true;
            CheckSlope();
        }
        else
        {
            acceleration = airAcceleration;
            grounded = false;
        }
    }
    void CheckSlope()
    {
        //throw a raycast in each direction from the ground check position to see if the player is near/on a slope, 
        //if you stop at the bottom of a slope a single raycast straight down doesn't hit, 
        //a sphere cast doesn't continuously detect collision so it will recognize the slope once on contact and none after,
        //sphere overlap detects well but I can't be bothered to implement that and I doubt it would be more efficent
        Ray frontCheck = new Ray(groundCheck.position, transform.forward);
        Ray backCheck = new Ray(groundCheck.position, -transform.forward);
        Ray rightCheck = new Ray(groundCheck.position, transform.right);
        Ray leftCheck = new Ray(groundCheck.position, -transform.right);
        Ray downCheck = new Ray(groundCheck.position, -transform.up);
        RaycastHit slopeCheck;

        if (Physics.Raycast(backCheck, out slopeCheck, .5f, whatIsGround) 
            || Physics.Raycast(rightCheck, out slopeCheck, .5f, whatIsGround) 
            || Physics.Raycast(leftCheck, out slopeCheck, .5f, whatIsGround)
            || Physics.Raycast(frontCheck, out slopeCheck, .5f, whatIsGround)
            || Physics.Raycast(downCheck, out slopeCheck, .5f, whatIsGround))
        {
            //adjust the direction and force of movement relative to the sloped surface
            if (slopeCheck.normal != Vector3.up && slopeLimit > Mathf.Abs(90 - Vector3.Angle(direction, slopeCheck.normal)))
            {
                onSlope = true;
                slopeAcceleration = Mathf.Max(acceleration, 15) * Mathf.Abs(90 - Vector3.Angle(direction, slopeCheck.normal)) * .07f;
                direction = Vector3.ProjectOnPlane(direction, slopeCheck.normal);
                //15 and .07 are arbitrary values found by trial and error, anything lower than this won't climb the slope
            }
            else
                onSlope = false;
        }
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(playerBody.position, .9f);
    }
}
