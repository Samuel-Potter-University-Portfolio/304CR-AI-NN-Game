﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;


/// <summary>
/// Read in a decision tree structure from an xml file
/// </summary>
public class DecisionTree
{
	/// <summary>
	/// The agent profiles that can be used with this tree
	/// </summary>
	private VariableCollection[] agentProfiles;

	/// <summary>
	/// The agent variables that are currently being used
	/// </summary>
	private VariableCollection agentProfile = new VariableCollection();
	/// <summary>
	/// The global variables that are avaliable to all profiles
	/// </summary>
	private VariableCollection globalVars = new VariableCollection();

	/// <summary>
	/// All actions which can be performed by this tree
	/// </summary>
	private Dictionary<string, ActionCallback> actionStates = new Dictionary<string, ActionCallback>();

	/// <summary>
	/// The root of the decision tree
	/// </summary>
	private IDecision rootDecision;

	/// <summary>
	/// The action to currently execute
	/// </summary>
	private ActionCallback currentAction;



	public DecisionTree()
	{
	}

	public void DebugMake()
	{
		DecisionIf ifDec = new DecisionIf("AliveCount", "32", DecisionIf.Operand.Equals);
		ifDec.trueDecision = new DecisionState(actionStates["Skirt"]);
		ifDec.falseDecision = new DecisionState(actionStates["Flee"]);
		rootDecision = ifDec;
	}
	
	/// <summary>
	/// Execute the current action
	/// </summary>
	public void Run()
	{
		if (currentAction != null)
			currentAction();
	}

	/// <summary>
	/// Run through the tree and update the current action
	/// </summary>
	public void Recalculate()
	{
		currentAction = rootDecision != null ? rootDecision.Process(globalVars, agentProfile) : null;
	}

	/// <summary>
	/// Register a specific action with this tree
	/// </summary>
	/// <param name="name">The unique name to use for this action</param>
	/// <param name="callback">The function to callback when this state is called</param>
	public void RegisterActionState(string name, ActionCallback callback)
	{
		actionStates[name] = callback;
	}

	/// <summary>
	/// Sets the value of a global variable
	/// </summary>
	/// <param name="name">The name of this variable</param>
	/// <param name="value">The value to give this variable</param>
	public void SetGlobalVar(string name, float value)
	{
		globalVars.SetVar(name, value);
	}

	/// <summary>
	/// Gets the global variable of this name
	/// </summary>
	/// <param name="name">The name of this variable</param>
	/// <param name="defaultValue">The default value to return in the event it is not found</param>
	/// <returns></returns>
	public float GetGlobalVar(string name, float defaultValue = 0.0f)
	{
		return globalVars.GetVar(name, defaultValue);
	}


	/// <summary>
	/// Read the XML file for a specific tree
	/// </summary>
	/// <param name="file">The path of the file to load</param>
	/// <returns></returns>
	public bool LoadXML(string file)
	{
		if (!System.IO.File.Exists(file))
			return false;


		XmlReaderSettings settings = new XmlReaderSettings();
		settings.IgnoreComments = true;

		XmlReader reader = XmlReader.Create(file, settings);

		XmlDocument document = new XmlDocument();
		document.Load(reader);

		// Read document
		XmlElement root = document.DocumentElement;
		foreach (XmlElement child in root.ChildNodes)
		{
			// Profile loading - TODO
			if (child.Name == "Profile")
			{
				if(child.HasAttribute("weight"))
					child.GetAttribute("weight");
			}

			// Load the actual tree
			if (child.Name == "Decision")
			{
				if (child.ChildNodes.Count != 1)
					Debug.LogError("Decision must have exactly 1 child");

				rootDecision = ParseDecision((XmlElement)child.FirstChild);
			}
		}

		Debug.Log("Read from '" + file + "'");
		return true;
	}

	/// <summary>
	/// Parse this xml node to create a decision
	/// </summary>
	/// <param name="node"></param>
	/// <returns></returns>
	private IDecision ParseDecision(XmlElement node)
	{
		if (node.Name == "if")
		{
			// Parse arguments
			if (!node.HasAttribute("lhs"))
				Debug.LogError("if missing attribute 'lhs'");
			if (!node.HasAttribute("rhs"))
				Debug.LogError("if missing attribute 'rhs'");
			if (!node.HasAttribute("operand"))
				Debug.LogError("if missing attribute 'operand'");

			DecisionIf.Operand operand = DecisionIf.Operand.Equals;
			string rawOperand = node.GetAttribute("operand");
			switch (rawOperand)
			{
				case "equal":
					operand = DecisionIf.Operand.Equals;
					break;
				case "notEqual":
					operand = DecisionIf.Operand.NotEquals;
					break;

				case "greaterThan":
					operand = DecisionIf.Operand.GreaterThan;
					break;
				case "greaterThanEqual":
					operand = DecisionIf.Operand.GreaterThanEquals;
					break;

				case "lessThan":
					operand = DecisionIf.Operand.LessThan;
					break;
				case "lessThanEqual":
					operand = DecisionIf.Operand.LessThanEquals;
					break;

				default:
					Debug.LogError("Unrecognised if operand '" + rawOperand + "'");
					break;
			}


			DecisionIf decision = new DecisionIf(node.GetAttribute("lhs"), node.GetAttribute("rhs"), operand);

			// Parse sub tree
			if (node.ChildNodes.Count > 2)
				Debug.LogError("if statements may only have 2 child nodes");

			foreach (XmlElement child in node.ChildNodes)
			{
				if (child.Name == "true")
				{
					if (decision.trueDecision != null)
						Debug.LogWarning("Multiple true entries for if");

					if (child.HasChildNodes)
						decision.trueDecision = ParseDecision((XmlElement)child.FirstChild);
				}
				else if (child.Name == "false")
				{
					if (decision.falseDecision != null)
						Debug.LogWarning("Multiple false entries for if");

					if (child.HasChildNodes)
						decision.falseDecision = ParseDecision((XmlElement)child.FirstChild);

				}
				else
					Debug.LogError("Unrecognised if child '" + child.Name + "'");
			}

			return decision;
		}

		else if (node.Name == "range")
		{
			// Parse arguments
			if (!node.HasAttribute("var"))
				Debug.LogError("range missing attribute 'var'");
			if (!node.HasAttribute("type"))
				Debug.LogError("range missing attribute 'type'");

			string rawType = node.GetAttribute("type");
			if (rawType != "greaterThan" && rawType != "lessThan")
				Debug.LogError("Unrecognised range type '" + rawType + "'");

			DecisionRange decision = new DecisionRange(node.GetAttribute("var"), rawType == "greaterThan");

			// Parse sub tree
			foreach (XmlElement child in node.ChildNodes)
			{
				if (child.Name == "limit")
				{
					if (!child.HasAttribute("value"))
						Debug.LogError("range.limit missing attribute 'value'");

					float max;
					float.TryParse(child.GetAttribute("value"), out max);

					if(child.HasChildNodes)
						decision.AddRange(max, ParseDecision((XmlElement)child.FirstChild));
				}
				else if(child.Name == "default")
				{
					if (decision.defaultDecision != null)
						Debug.LogWarning("Multiple default entries for range");

					if (child.HasChildNodes)
						decision.defaultDecision = ParseDecision((XmlElement)child.FirstChild);
				}
				else
					Debug.LogError("Unrecognised range child '" + child.Name + "'");
			}

			return decision;
		}

		else if (node.Name == "state")
		{
			if(!node.HasAttribute("action"))
				Debug.LogError("state requires 'action' attribute");

			string actionName = node.GetAttribute("action");
			if (!actionStates.ContainsKey(actionName))
				Debug.LogError("action '" + actionName + "' not recognised");

			return new DecisionState(actionStates[actionName]);
		}
		else
		{
			Debug.LogError("Unrecognised Decision method '" + node.Name + "'");
			return null;
		}
	}
}
