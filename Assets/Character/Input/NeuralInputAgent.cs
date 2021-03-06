﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Collection of normalized pixels which a network can see
/// </summary>
public struct NeuralPixel
{
    public bool containsStage;
    public bool containsCharacter;
    public bool containsArrow;
	public bool containsShield;

	///
	/// This data should be passed into the network as 2 floats
	/// stage and character will share a float [-1: stage 1: character]
	/// arrow and shield will share a float [-1: shield 1: arrow]
	///
}


/// <summary>
/// Character input profile which uses a NEAT net to controller it
/// </summary>
[RequireComponent(typeof(Character))]
public class NeuralInputAgent : MonoBehaviour
{
    public static int ViewResolution { get { return 16; } }

	/// <summary>
	/// The number of inputs dedicated to the rendered view
	/// </summary>
    public static int ResolutionInputCount { get { return ViewResolution * ViewResolution * 2; } }

	/// <summary>
	/// The total number of inputs this agent requires
	/// </summary>
	public static int InputCount { get { return ResolutionInputCount + 9; } }
	/// <summary>
	/// The total number of outputs this agent requires
	/// </summary>
	public static int OutputCount { get { return 4; } }


	public Character character { get; private set; }
	private Transform cachedTrans;
    
    public NeuralPixel[,] display { get; private set; }
	public float displayScale = 1.5f;

	/// <summary>
	/// The network that this profile is currently using
	/// </summary>
	public NeatNetwork network;
	private float[] networkInput;
	private float[] networkOutput;

	/// <summary>
	/// How long this agent has survived
	/// </summary>
	public float survialTime { get; private set; }
	/// <summary>
	/// How many kills this agent has gotten
	/// </summary>
	public int killCount { get { return character ? character.killCount : 0; } }
	/// <summary>
	/// How many blocks this agent has gotten
	/// </summary>
	public int blockCount { get { return character ? character.blockCount : 0; } }


	void Start ()
    {
        character = GetComponent<Character>();
		cachedTrans = transform;

		display = new NeuralPixel[ViewResolution, ViewResolution];
		networkInput = new float[InputCount];
	}
	
    void FixedUpdate ()
    {
		if (network == null)
			return;

		// Set network fitness from survival and kill count
		network.fitness = 
			survialTime				* NeuralController.main.survialWeight + 
			killCount				* NeuralController.main.killWeight +
			blockCount				* NeuralController.main.blockWeight +
			character.roundWinCount * NeuralController.main.winnerWeight;

		// Don't run network for dead agents
		if (character.IsDead)
			return;
		
		// Update stats
		survialTime += Time.fixedDeltaTime;


		// Update inputs
		RenderVision();

		// Shoot Cooldown
		networkInput[ResolutionInputCount + 0] = character.NormalizedShootTime * 2.0f - 1.0f;
		// Sheild Cooldown
		networkInput[ResolutionInputCount + 1] = character.NormalizedShieldReuseTime * 2.0f - 1.0f;
		// Sheild stun
		networkInput[ResolutionInputCount + 2] = character.NormalizedShieldStunTime * 2.0f - 1.0f;
		// Stage Size
		networkInput[ResolutionInputCount + 3] = (GameMode.main.stage.currentSize / GameMode.main.stage.DefaultSize) * 2.0f - 1.0f;
		// Rotation Sin
		networkInput[ResolutionInputCount + 4] = Mathf.Cos(character.directionAngle);
		// Rotation Cos
		networkInput[ResolutionInputCount + 5] = Mathf.Sin(character.directionAngle);
		// Alive populations
		networkInput[ResolutionInputCount + 6] = ((float)GameMode.main.aliveCount / (float)GameMode.main.characterCount) * 2.0f - 1.0f;
		// Bias input node
		networkInput[ResolutionInputCount + 7] = 1.0f;
		// Bias input node
		networkInput[ResolutionInputCount + 8] = -1.0f;

		// Run network
		networkOutput = network.GenerateOutput(networkInput);

		// Output 0: Move
		character.Move(networkOutput[0]);
		// Output 1: Turn
		character.Turn(networkOutput[1]);
		// Output 2: Shoot 
		if (networkOutput[2] >= 0.5f)
			character.Fire();
		// Output 3: Block 
		if (networkOutput[3] >= 0.5f)
			character.Block();
	}

	public void AssignNetwork(NeatNetwork network)
	{
		if (character == null)
			character = GetComponent<Character>();

		this.network = network;
		if (network != null)
			character.SetColour(network.assignedSpecies.colour);
		else
			character.SetColour(Color.black);
	}


#if UNITY_EDITOR
	void OnDrawGizmosSelected()
	{
		// DEBUG Draw view areas
		Vector3 a = transform.position + (transform.forward * (ViewResolution / 2) + transform.right * (ViewResolution / 2)) * displayScale;
		Vector3 b = transform.position + (transform.forward * (ViewResolution / 2) + transform.right * (-ViewResolution / 2)) * displayScale;
		Vector3 c = transform.position + (transform.forward * (-ViewResolution / 2) + transform.right * (ViewResolution / 2)) * displayScale;
		Vector3 d = transform.position + (transform.forward * (-ViewResolution / 2) + transform.right * (-ViewResolution / 2)) * displayScale;

		Gizmos.color = Color.red;
		Gizmos.DrawLine(a, b);
		Gizmos.DrawLine(c, d);

		Gizmos.DrawLine(a, c);
		Gizmos.DrawLine(b, d);
	}
#endif

	/// <summary>
	/// Update the input for what this net can currently see
	/// </summary>
	void RenderVision()
	{
		StageController stage = GameMode.main.stage;
		Vector3 stageCentre = stage.transform.position;
		float stageSize = stage.currentSize + 0.25f;

		Vector3 forward = cachedTrans.forward;
		Vector3 right = cachedTrans.right;


		// Arena view
		for (int x = 0; x < ViewResolution; ++x)
			for (int y = 0; y < ViewResolution; ++y)
			{
				display[x, y] = new NeuralPixel(); // Reset pixel

				Vector2 dir = new Vector2(x - ViewResolution / 2, y - ViewResolution / 2) * displayScale;
				Vector3 check = cachedTrans.position + forward * dir.y + right * dir.x;

				// Calculate if inside of circle
				float a = check.x - stageCentre.x;
				float b = check.z - stageCentre.z;
				if (a * a + b * b <= stageSize * stageSize)
					display[x, y].containsStage = true;
			}

		// Display characters
		foreach (Character other in GameMode.main.characters)
		{
			if (other.IsDead)
				continue;

			// Draw character
			Vector2Int pos = WorldToRender(other.transform.position);

			if (pos.x >= 0 && pos.x < ViewResolution && pos.y >= 0 && pos.y < ViewResolution)
				display[pos.x, pos.y].containsCharacter = true;

			// Draw arrow
			if (other.currentProjectile != null)
			{
				pos = WorldToRender(other.currentProjectile.transform.position);

				if (pos.x >= 0 && pos.x < ViewResolution && pos.y >= 0 && pos.y < ViewResolution)
					display[pos.x, pos.y].containsArrow = true;
			}

			// Draw sheild
			if (other.currentShield != null && other.currentShield.IsActive)
			{
				pos = WorldToRender(other.currentShield.transform.position);

				if (pos.x >= 0 && pos.x < ViewResolution && pos.y >= 0 && pos.y < ViewResolution)
					display[pos.x, pos.y].containsShield = true;
			}
		}


		// Update inputs
		for (int x = 0; x < ViewResolution; ++x)
			for (int y = 0; y < ViewResolution; ++y)
			{
				int arrowShieldIndex = x + y * ViewResolution;
				int characterStageIndex = ViewResolution * ViewResolution + x + y * ViewResolution;
				NeuralPixel pixel = display[x, y];

				networkInput[arrowShieldIndex] = pixel.containsArrow ? 1.0f : pixel.containsShield ? -1.0f : 0.0f;
				networkInput[characterStageIndex] = pixel.containsCharacter ? 1.0f : pixel.containsStage ? -1.0f : 0.0f;
			}
	}

	/// <summary>
	/// Convert a world position into a render pixel position
	/// </summary>
	public Vector2Int WorldToRender(Vector3 position)
	{
		Vector3 pos = position - cachedTrans.position;

		int x = Mathf.RoundToInt(Vector3.Dot(pos, cachedTrans.right) / displayScale) + ViewResolution / 2;
		int y = Mathf.RoundToInt(Vector3.Dot(pos, cachedTrans.forward) / displayScale) + ViewResolution / 2;
		return new Vector2Int(x, y);
	}

}
