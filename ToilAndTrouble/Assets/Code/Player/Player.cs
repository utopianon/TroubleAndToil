﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    [Header("Controller options")]
    public bool platformerMovement;
    public bool variableJumping;


    CharacterController controller;
    Animator anim;

    [Space(10f)]
    [SerializeField] private float moveSpeed = 6;
    [SerializeField] private float minJumpHeight = 1;
    [SerializeField] private float maxJumpHeight = 4;
    [SerializeField] private float minJumpDist = 2;
    [SerializeField] private float maxJumpDist = 6;
    [SerializeField] private float timeToJumpPeak = .4f;
    [SerializeField] private float jumpFallModifier = 2;
    [SerializeField] private float accelerationTimeInAir = .2f;
    [SerializeField] private float accelerationTimeGrounded = .1f;
    [SerializeField] private float minimumVelocity = 4;
    [SerializeField] private float maximumVelocity = 12;

    public Vector2 wallJumpClimb;
    public Vector2 wallJumpOff;
    public Vector2 wallJumpLeap;
    private bool wallSliding = false;
    private int wallDirX;
    public float wallSlideSpeedMax = 3;
    public float wallStickTime = .25f;
    public float timeToWallUnstick;

    public AttachPoint[] attachPoints = new AttachPoint[3];

    private float gravity;
    private float baseGravity;
    private float minJumpVelocity;
    private float maxJumpVelocity;
    private Vector3 velocity, oldPos;
    float movementSmoothing;
    private Vector2 input;    

    private float _deathValueX = 0.0001f;
    bool died = false;

    CameraFollow cameraFollow;

    // casting stuff
    Vector2 castSize;

    public int bufferSize = 12;     //How many frames the input buffer keeps checking for new inputs / The Size of the Buffer
    public InputBufferItem[] inputBuffer;

    SpriteRenderer spriteRenderer;

    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<CharacterController>();
        anim = GetComponentInChildren<Animator>();
        attachPoints = GetComponentsInChildren<AttachPoint>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        //calculate gravity and velocity from wanted jump height and time to peak
        gravity = -(2 * maxJumpHeight / Mathf.Pow(timeToJumpPeak, 2));
        maxJumpVelocity = Mathf.Abs(gravity) * timeToJumpPeak;
        minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(gravity) * minJumpHeight);
        print("Gravity: " + gravity + " Jump Velocity: " + maxJumpVelocity);
        baseGravity = gravity;

        cameraFollow = Camera.main.GetComponent<CameraFollow>();

        inputBuffer = new InputBufferItem[bufferSize];
        for (int i = 0; i < inputBuffer.Length; i++)
        {
            inputBuffer[i] = new InputBufferItem();
        }
    }

    // Update is called once per frame
    private void Update()
    {
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        //wall direction for the type of wall jump
        wallDirX = (controller.collisions.left) ? -1 : 1;

        float targetVelocityX = input.x * moveSpeed;
        velocity.x = Mathf.SmoothDamp(velocity.x, targetVelocityX, ref movementSmoothing, (controller.collisions.below) ? accelerationTimeGrounded : accelerationTimeInAir);

        wallSliding = false;

        if ((controller.collisions.left || controller.collisions.right) && !controller.collisions.below && velocity.y < 0)
        {
            wallSliding = true;
            //gravity = baseGravity;

            if (velocity.y < -wallSlideSpeedMax && wallDirX == input.x)
            {
                velocity.y = -wallSlideSpeedMax;
            }

            if (timeToWallUnstick > 0)
            {
                movementSmoothing = 0;
                velocity.x = 0;

                if (input.x != wallDirX && input.x != 0)
                {
                    timeToWallUnstick -= Time.deltaTime;
                }
                else
                {
                    timeToWallUnstick = wallStickTime;
                }
            }
            else
            {
                timeToWallUnstick = wallStickTime;
            }
        } 

        if (controller.collisions.above || controller.collisions.below)
        {
            velocity.y = 0;
        }

        UpdateBuffer();
        UpdateCommand();


        if (variableJumping)
        {
            if (Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.UpArrow))
            {
                if (velocity.y > (maxJumpVelocity / 2))
                    velocity.y = (maxJumpVelocity / 2);
            }
        }

        if (!controller.collisions.below && !wallSliding && velocity.y < 0 && gravity == baseGravity)
        {
            gravity = gravity * jumpFallModifier;
        }
        else
        {
            gravity = baseGravity;
        }

        velocity.y += gravity * Time.deltaTime;
        oldPos = transform.position;
        if (controller.collisions.faceDir > 0)
        {
            spriteRenderer.flipX = true;
        }
        else
        {
            spriteRenderer.flipX = false;
        }

        StandartMovement();
    }

    void UpdateBuffer()
    {
        if (Input.GetAxisRaw("Jump") > 0) { inputBuffer[inputBuffer.Length - 1].Hold(); }
        else { inputBuffer[inputBuffer.Length - 1].ReleaseHold(); }

        //Go through each Input Buffer item and copy the previous frame
        for (int i = 0; i < inputBuffer.Length - 1; i++)
        {
            inputBuffer[i].hold = inputBuffer[i + 1].hold;
            inputBuffer[i].used = inputBuffer[i + 1].used;
        }
    }

    public void UpdateCommand()
    {
        for (int i = 0; i < inputBuffer.Length; i++)
        {
            if (inputBuffer[i].CanExecute())
            {
                if (wallSliding)
                {
                    if (wallDirX == input.x)
                    {
                        velocity.x = -wallDirX * wallJumpClimb.x;
                        velocity.y = wallJumpClimb.y;
                    }

                    else if (input.x == 0)
                    {
                        velocity.x = -wallDirX * wallJumpOff.x;
                        velocity.y = wallJumpOff.y;
                    }

                    else
                    {
                        velocity.x = -wallDirX * wallJumpLeap.x;
                        velocity.y = wallJumpLeap.y;
                    }
                }

                if (controller.collisions.below)
                {
                    FastFallingJump();
                    inputBuffer[i].Execute();
                    break;
                }
            }
        }
    } 

    void StandartMovement()
    {
        controller.Move(velocity * Time.deltaTime);
    }

    void FastFallingJump()
    {
        velocity.y = maxJumpVelocity;
     //   StartCoroutine(DoFallJump());
    }


    //public void DoDangerAndPickup()
    // {
    //     RaycastHit2D[] hits = Physics2D.BoxCastAll(oldPos, castSize, 0f, velocity.normalized, velocity.magnitude * Time.fixedDeltaTime);

    //     for (int i = 0; i < hits.Length; i++)
    //     {
    //         Pickup pickup = hits[i].collider.GetComponent<Pickup>();
    //         if (pickup != null)
    //         {
    //             if (pickup.victoryPickup)
    //             { 
    //                 //win level
    //                 won = true;
    //                 platformerMovement = true;
    //                 GameManager.GM.EndScreen(true);
    //             }
    //             GameManager.GM.currentScore += pickup.score;
    //             pickup.Die();
    //         }

    //         Danger danger = hits[i].collider.GetComponent<Danger>();
    //         if (danger != null)
    //         {
    //             died = true;
    //         }
    //     }

    //     if (died && !won) Die();
    // }

    void Die()
    {
        // controller.colliding = false;
        //  GameManager.GM.EndScreen(false);
    }

    private IEnumerator DoFallJump()
    {

        while (velocity.y > 0)
        {
            yield return null;
        }
        gravity = gravity * jumpFallModifier;
        while (!controller.collisions.below)
        {
            yield return null;

        }
        gravity = baseGravity;
        yield return null;
    }

}
