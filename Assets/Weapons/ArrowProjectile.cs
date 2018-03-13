﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
public class ArrowProjectile : MonoBehaviour
{
    private Rigidbody body;

	[SerializeField]
	private Transform animatedTransform;
	[SerializeField]
	private float speed = 10.0f;

	private Character owner;
    private bool inFlight = false;

	/// <summary>
	/// How long until this arrow is destroyed
	/// </summary>
	private float lifeTimer;


	
	void Update ()
    {
		// Point arrow model in direction travelling
		if (inFlight)
			animatedTransform.forward = body.velocity;
		else if (lifeTimer > 3.0f)
			lifeTimer = 3.0f;


		lifeTimer -= Time.deltaTime;
		if (lifeTimer <= 0.0f)
			Destroy(gameObject);
	}

    public void Fire(Character archer)
    {
        owner = archer;
        Vector2 aim = archer.direction;

		body = GetComponent<Rigidbody>();
		body.velocity = new Vector3(aim.x * speed, 2.0f, aim.y * speed);
        transform.position = archer.transform.position + new Vector3(aim.x, 0, aim.y) * 1.6f;

        inFlight = true;
		lifeTimer = 8.0f;

	}

    void OnCollisionEnter(Collision collision)
    {
		// Stop arrow
		if (collision.gameObject.CompareTag("Stage"))
		{
			inFlight = false;
			GetComponent<Collider>().enabled = false;
			body.isKinematic = true;
		}

		// Attempt to attack another character
		else if(inFlight)
		{
			Character character = collision.gameObject.GetComponent<Character>();

			// Hit character
			if (character != null)
			{
				owner.OnGoodShot(character);
				character.OnBeenShot(owner);
			}
		}
    }
}