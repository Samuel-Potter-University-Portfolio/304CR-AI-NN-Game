﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Animator))]
public class CubeAnimator : MonoBehaviour
{
    private Animator animator;

    [SerializeField]
    private Character character;

    private int tag_AbsSpeed;
    private int tag_Speed;
	private int tag_IsFiring;
	private int tag_FireDuration;
	private int tag_IsDead;


	void Start ()
    {
        animator = GetComponent<Animator>();

        tag_AbsSpeed = Animator.StringToHash("AbsSpeed");
        tag_Speed = Animator.StringToHash("Speed");
		tag_IsFiring = Animator.StringToHash("IsFiring");
		tag_FireDuration = Animator.StringToHash("FireDuration");
		tag_IsDead = Animator.StringToHash("IsDead");
	}
	
	void Update ()
    {
        float dir = 1.0f;
        if (Vector2.Dot(character.velocity, character.direction) < 0.0f)
            dir = -1.0f;

        float speed = Mathf.Clamp01(character.velocity.magnitude);

        animator.SetFloat(tag_AbsSpeed, speed);
        animator.SetFloat(tag_Speed, speed * dir);

        animator.SetBool(tag_IsFiring, character.currentAction == Character.Action.Shooting);
		animator.SetFloat(tag_FireDuration, 1.0f / character.shootDuration);

		animator.SetBool(tag_IsDead, character.IsDead);
	}
}
