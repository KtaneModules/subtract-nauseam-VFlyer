using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using KModkit;
using Random = UnityEngine.Random;
using System.Text.RegularExpressions;

public class SubtractNauseamScript : MonoBehaviour {

	public KMSelectable[] directionsSelectable;
	public KMAudio mAudio;
	public KMBombModule modSelf;
	public KMBombInfo info;
	public TextMesh[] directionsText;
	public TextMesh centerMesh, timerMesh, statusMesh;

	static readonly string[] debugDirections = { "Up", "Right", "Down", "Left" },
		debugQuestionType = { "Q#", "ODD/EVEN", "MIN", "MAX", "Xo#", "#o#" },
		directionSymbols = { "\u2191", "\u2192", "\u2193", "\u2190" };
	static readonly string[] symbolsGrid = {
		"----$----",
		"---&£#---",
		"--£#%$&--",
		"-#%$&£#%-",
		"%$&£#%$&£",
		"-£#%$&£#-",
		"--$&£#%--",
		"---#%$---",
		"----&----",
	};


	static int modIDcnt;
	int modID;
	List<Result> allResults = new List<Result>();
	/*
	List<bool> lastAttemptsSuccessful = new List<bool>();
	List<float> lastAttemptsTimeTaken = new List<float>();
	List<int> lastAttemptsQuestionsAnswered = new List<int>();
	List<int[]> lastAttemptsExpectedFinalAnswer = new List<int[]>();
	List<int[]> lastAttemptsSubmittedFinalAnswer = new List<int[]>();
	*/
	float timeTaken;
	bool hasStarted = false, isAnimating, firstActivation = false, modSolved = false, showingPassword = false, allowTPSayPrompt;
	int[] generatedIndividualDigits, generatedDirectionIdxes, allQuestionIDxType, expectedSubmissionIdx, currentSubmissionIdx, directionInputDigits;
	int curQuestionIdx = 0, lastHighlightedIdx = -1, curResultHighlightIdx;
	string[] lastArrowDisplayedTexts;
	string selectedCharacters = "0123", tpMessagePrompt;
	IEnumerator timeTicker;
	// Use this for initialization
	void Start()
	{
		modID = ++modIDcnt;
		for (var x = 0; x < directionsSelectable.Length; x++)
		{
			var y = x;
			directionsSelectable[x].OnInteract += delegate {
				directionsSelectable[y].AddInteractionPunch(0.5f);
				if (!isAnimating)
				{
					if (hasStarted)
						HandleCurrentPressInQuizMode(y);
					else if (!firstActivation)
					{
						firstActivation = true;
						StartAttempt();
					}
					else
                    {
						HandlePressDuringStatus(y);
                    }
				}
				return false;
			};
			directionsSelectable[x].OnHighlight += delegate {

				if (hasStarted && !isAnimating && curQuestionIdx < 10)
				{
					lastHighlightedIdx = y;
					HandleHighlightAnswer(y);
				}
			};
			directionsSelectable[x].OnHighlightEnded += delegate {
				if (hasStarted && !isAnimating && curQuestionIdx < 10)
					HandleHighlightAnswer();
			};
		}
		GenerateStuff();
	}
	void QuickLog(string value, params object[] args)
	{
		Debug.LogFormat("[Subtract Nauseam #{0}] {1}", modID, string.Format(value, args));
	}
	void CreateCurQuestion()
	{
		if (lastArrowDisplayedTexts == null)
			lastArrowDisplayedTexts = new string[4];
		var incorrectDirectionIdxes = Enumerable.Range(0, 4).Where(a => generatedDirectionIdxes[curQuestionIdx] != a);
		var curCorrectValue = generatedIndividualDigits[curQuestionIdx];
		var incorrectDigits = Enumerable.Range(0, 10).Where(a => a != curCorrectValue).ToArray().Shuffle();
		var possibleCenterText = "OOPS";
		switch (allQuestionIDxType[curQuestionIdx])
		{
			case 0:
				{
					possibleCenterText = "Q#";
				}
				goto default;
			case 1:
				{
					incorrectDigits = incorrectDigits.Where(a => a % 2 != curCorrectValue % 2).ToArray();
					possibleCenterText = curCorrectValue % 2 == 1 ? "ODD" : "EVEN";
				}
				goto default;
			case 2:
				{
					incorrectDigits = incorrectDigits.Where(a => a > curCorrectValue).ToArray();
					possibleCenterText = "MIN";
				}
				goto default;
			case 3:
				{
					incorrectDigits = incorrectDigits.Where(a => a < curCorrectValue).ToArray();
					possibleCenterText = "MAX";
				}
				goto default;
			case 4:
				{
					var lastValue = generatedIndividualDigits[curQuestionIdx - 1];
					possibleCenterText = string.Format("X{0}{1}", lastValue - curCorrectValue > 0 ? "-" : lastValue - curCorrectValue < 0 ? "+" : "", lastValue == curCorrectValue ? "" : lastValue >= curCorrectValue ? (lastValue - curCorrectValue).ToString() : (curCorrectValue - lastValue).ToString());
				}
				goto default;
			case 5:
				{
					var modifier = Random.Range(1, 10);
					var possibleExpressions = new[] {
						string.Format("{0}+{1}", curCorrectValue - modifier, modifier),
						string.Format("{0}/{1}", curCorrectValue * modifier, modifier),
						string.Format("{0}-{1}", curCorrectValue + modifier, modifier),
					};
					var selectedIdxPossible = Enumerable.Range(0, 3).Where(a => a != 0 || curCorrectValue - modifier > 0);
					possibleCenterText = possibleExpressions[selectedIdxPossible.PickRandom()];
				}
				goto default;
			default:
				centerMesh.text = possibleCenterText;
				statusMesh.text = string.Format("{0}/10", allQuestionIDxType[curQuestionIdx] != 0 ? (curQuestionIdx + 1).ToString("00") : "??");
				lastArrowDisplayedTexts = directionsText.Select(a => a.text).ToArray();
				for (var x = 0; x < incorrectDirectionIdxes.Count(); x++)
					lastArrowDisplayedTexts[incorrectDirectionIdxes.ElementAt(x)] = incorrectDigits[x].ToString();
				lastArrowDisplayedTexts[generatedDirectionIdxes[curQuestionIdx]] = curCorrectValue.ToString();
				for (var x = 0; x < directionsText.Length; x++)
					directionsText[x].text = lastArrowDisplayedTexts[x];
				HandleHighlightAnswer(lastHighlightedIdx);
				QuickLog("Showing prompt \"{0}\" with following answers: {1}", possibleCenterText, Enumerable.Range(0, 4).Select(a => "[" + debugDirections[a] + ": " + lastArrowDisplayedTexts[a] + "]").Join(", "), curQuestionIdx + 1);
                tpMessagePrompt = string.Format("Prompt: \"{0}\" Possible Answers: {1}", possibleCenterText, Enumerable.Range(0, 4).Select(a => "[" + debugDirections[a].First() + ": " + lastArrowDisplayedTexts[a] + "]").Join(", "));
				break;
		}
	}
	void HandleCurrentPressInQuizMode(int idxDirectionPressed)
	{
		if (curQuestionIdx < 10)
		{
			if (generatedDirectionIdxes[curQuestionIdx] == idxDirectionPressed)
			{
				curQuestionIdx++;
				mAudio.PlaySoundAtTransform("Right", directionsSelectable[idxDirectionPressed].transform);
				if (curQuestionIdx < 10)
					CreateCurQuestion();
				else
					StartSubmission();
			}
			else
			{
				QuickLog("Direction {0} was incorrectly pressed on prompt #{1}.", debugDirections[idxDirectionPressed], curQuestionIdx + 1);
				mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, directionsSelectable[idxDirectionPressed].transform);
				ResetModule();
			}
		}
		else
		{
			mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, directionsSelectable[idxDirectionPressed].transform);
			if (directionInputDigits[idxDirectionPressed] == 3)
			{
				QuickLog("The symbol representing the submit button was pressed and submitted the following passcode: {0}", currentSubmissionIdx.Select(a => a < 0 || a >= selectedCharacters.Length ? "-" : selectedCharacters.Substring(a, 1)).Join(""));
				if (currentSubmissionIdx.SequenceEqual(expectedSubmissionIdx))
				{
					if (!modSolved)
					{
						modSolved = true;
						modSelf.HandlePass();
						QuickLog("At this point, the module has been disarmed. Further attempts will be logged underneath this, with fake strikes only being noted.");
					}
					if (allResults.Any() || Random.value > 0.2f || timeTaken >= 80f)
						mAudio.PlaySoundAtTransform("Good", transform);
					else
						mAudio.PlaySoundAtTransform("Best", transform);
					StopCoroutine(timeTicker);
					var subResult = new Result(true, timeTaken, curQuestionIdx, expectedSubmissionIdx.ToArray(), currentSubmissionIdx.ToArray());
					allResults.Add(subResult);
					curResultHighlightIdx = allResults.Count - 1;
					ShowStatus();
					GenerateStuff();
					hasStarted = false;
				}
				else
				{
					ResetModule();
				}
			}
			else
			{
				for (var x = 0; x < currentSubmissionIdx.Length - 1; x++)
				{
					currentSubmissionIdx[x] = currentSubmissionIdx[x + 1];
				}
				currentSubmissionIdx[3] = directionInputDigits[idxDirectionPressed];
				statusMesh.text = currentSubmissionIdx.Select(a => a < 0 || a >= selectedCharacters.Length ? "-" : selectedCharacters.Substring(a, 1)).Join("");
			}
		}
	}
	void GenerateStuff()
    {
		if (generatedDirectionIdxes == null)
			generatedDirectionIdxes = new int[10];
		if (generatedIndividualDigits == null)
			generatedIndividualDigits = new int[10];
		if (expectedSubmissionIdx == null)
			expectedSubmissionIdx = new int[4];
		if (currentSubmissionIdx == null)
			currentSubmissionIdx = new int[4];
		if (directionInputDigits == null)
			directionInputDigits = Enumerable.Range(0, 4).ToArray();
		// Generate a pooled set of questions.
		allQuestionIDxType = new[] { 0, 1, 1, 2, 3, 4, 5, 5, 5, 5 };
		// 0: Q#, 1: ODD/EVEN, 2: MIN, 3: MAX, 4: Xo#, 5: #o#
		do
			allQuestionIDxType.Shuffle();
		while (allQuestionIDxType.First() == 4);
		for (var x = 0; x < allQuestionIDxType.Length; x++)
		{
			// Pregenerate all correct values on this module.
			switch (allQuestionIDxType[x])
			{
				case 0:
					generatedIndividualDigits[x] = (x + 1) % 10;
					break;
				case 1:
					generatedIndividualDigits[x] = Enumerable.Range(0, 10).Where(a => a % 2 == allQuestionIDxType[x] % 2).PickRandom();
					break;
				case 2:
					generatedIndividualDigits[x] = Random.Range(0, 7);
					break;
				case 3:
					generatedIndividualDigits[x] = Random.Range(3, 10);
					break;
				default:
					generatedIndividualDigits[x] = Random.Range(0, 10);
					break;
			}
			// Pregenerate all correct directions on this module.
			generatedDirectionIdxes[x] = Random.Range(0, 4);
		}
		QuickLog("Generated value sequence: {0}", generatedIndividualDigits.Join(", "));
		QuickLog("Generated directions to press: {0}", generatedDirectionIdxes.Select(a => debugDirections[a]).Join(", "));
		QuickLog("Generated question types: {0}", allQuestionIDxType.Select(a => debugQuestionType[a]).Join(", "));
		var finalSum = generatedIndividualDigits.Sum();
		for (var x = 0; x < 4; x++)
		{
			var curProduct = 1;
			for (var y = 0; y < 3 - x; y++)
				curProduct *= 3;
			expectedSubmissionIdx[x] = finalSum / curProduct % 3;
			currentSubmissionIdx[x] = -1;
		}
		QuickLog("The last 4 ternary digits of the sum of the generated value sequence ({1}) is: {0}", expectedSubmissionIdx.Join(""), finalSum);
		directionInputDigits.Shuffle();
		// Calculate the correct password for this module.
		IEnumerable<int> outsideOffsets = new[] { 1, 5, 7, 3 };
		outsideOffsets = outsideOffsets.Skip(generatedDirectionIdxes[0]).Concat(outsideOffsets.Take(generatedDirectionIdxes[0]));

		var startColTL = 3;
		var startRowTL = 3;
		var appliedRulesIdx = new List<bool>();
		var appliedDirectionsIdx = new List<int>();

		for (var x = 0; x < 3; x++)
		{
			var cur3Directions = generatedDirectionIdxes.Skip(3 * x + 1).Take(3);
			var _3DistinctDirections = cur3Directions.Distinct().Count() == cur3Directions.Count();
			QuickLog("Direction set #{0}: {1} (3 {2} directions)", x + 1, cur3Directions.Select(a => debugDirections[a]).Join(", "), _3DistinctDirections ? "distinct" : "nondistinct");
			appliedRulesIdx.Add(_3DistinctDirections);
			var expectedDirectionToAdd = _3DistinctDirections ?
				Enumerable.Range(0, 4).Except(cur3Directions).Single() :
				Enumerable.Range(0, 4).Single(b => cur3Directions.Count(a => a == b) >= Enumerable.Range(0, 4).Max(c => cur3Directions.Count(d => d == c)));
			QuickLog("Expected to move in this direction: {0}", debugDirections[expectedDirectionToAdd]);
			if (x <= 0 || _3DistinctDirections || appliedRulesIdx[x - 1] || expectedDirectionToAdd != appliedDirectionsIdx[x - 1])
				appliedDirectionsIdx.Add(expectedDirectionToAdd);
			else
			{
				appliedDirectionsIdx.Add(-1);
				QuickLog("However, the last set has 3 nondistinct directions, this set has 3 nondistinct directions, and both are expected to move in the same direction. This expected direction is to stay in place.");
			}
		}
		QuickLog("Expected moves from the center: {0}", appliedDirectionsIdx.Where(a => Enumerable.Range(0, 4).Contains(a)).Select(a => debugDirections[a]).Join(", "));
		foreach (int appliedDirection in appliedDirectionsIdx)
		{
			switch (appliedDirection)
			{
				case 0:
					startRowTL--;
					break;
				case 1:
					startColTL++;
					break;
				case 2:
					startRowTL++;
					break;
				case 3:
					startColTL--;
					break;
			}
		}
		QuickLog("This gives the obtained 3x3 portion of the grid:");
		var stringObtainedCharacters = symbolsGrid.Skip(startRowTL).Take(3).Select(a => a.Substring(startColTL, 3)).Join("");
		for (var x = 0; x < 3; x++)
		{
			QuickLog(stringObtainedCharacters.Substring(3 * x, 3));
		}
		var finalObtainedChars = outsideOffsets.Select(a => stringObtainedCharacters[a]).Concat(new[] { stringObtainedCharacters[4] });
		QuickLog("Obtained characters: {0}", finalObtainedChars.Join(""));
		var voidedChar = finalObtainedChars.PickRandom();
		finalObtainedChars = finalObtainedChars.Where(a => a != voidedChar);
		QuickLog("Characters present on the module: {0}", finalObtainedChars.Join(""));

		var distinctSerialNoLetters = info.GetSerialNumberLetters().Distinct().Take(4);
		//Debug.Log(afterModifiedChars.Join(""));
		var orderedItems = Enumerable.Range(0, 4 - distinctSerialNoLetters.Count())
			.Concat(
			Enumerable.Range(0, distinctSerialNoLetters.Count()).OrderByDescending(a => distinctSerialNoLetters.ElementAt(a)).Select(a => a - distinctSerialNoLetters.Count() + 4)).ToArray();
		//Debug.Log(Enumerable.Range(0, 4).Select(a => a.ToString() + ":" + orderedItems.ElementAt(a)).Join("|"));
		selectedCharacters = Enumerable.Range(0, 4).Select(a => finalObtainedChars.ElementAt(orderedItems[a])).Join("");

		QuickLog("After ordering the symbols in relation to the serial number letters, the symbols are represented by the following: [{0}]",
			Enumerable.Range(0, 4).Select(a => a.ToString() + ": " + selectedCharacters[a]).Join("], ["));
		QuickLog("Expected passcode to submit: {0}", expectedSubmissionIdx.Select(a => selectedCharacters[a]).Join(""));
	}
	void ResetModule()
	{
		StopCoroutine(timeTicker);
		if (!modSolved)
		{
			modSelf.HandleStrike();
			if (allResults.Count(a => !a.isSuccessful) >= 3)
				mAudio.PlaySoundAtTransform("Vom", transform);
		}
		else
			mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.Strike, transform);
		hasStarted = false;
		timerMesh.text = "999";
		var subResult = new Result(false, timeTaken, curQuestionIdx, expectedSubmissionIdx.ToArray(), curQuestionIdx >= 10 ? currentSubmissionIdx.ToArray() : null);
		allResults.Add(subResult);
		curResultHighlightIdx = allResults.Count - 1;
		for (var x = 0; x < directionsText.Length; x++)
			directionsText[x].text = "";
		centerMesh.text = "";
		GenerateStuff();
		ShowStatus();
	}
	void HandlePressDuringStatus(int dirIdx)
    {
		switch (dirIdx)
		{
			case 2:
				StartAttempt();
				break;
			case 0:
				if (allResults[curResultHighlightIdx].questionsAnswered >= 10)
				{
					showingPassword = !showingPassword;
					goto default;
				}
				goto case 2;
			case 1:
				if (curResultHighlightIdx + 1 < allResults.Count)
				{
					curResultHighlightIdx = curResultHighlightIdx + 1;
					showingPassword = false;
					goto default;
				}
				goto case 2;

			case 3:
				if (curResultHighlightIdx > 0)
				{
					curResultHighlightIdx = curResultHighlightIdx - 1;
					showingPassword = false;
					goto default;
				}
				goto case 2;
			default:
				ShowStatus();
				break;
		}
	}
	void ShowStatus()
	{
		var curResultShown = allResults[curResultHighlightIdx];
		var timeGrades = new Dictionary<float, string>()
		{
			{ 1000, "D-" },
			{ 925, "D" },
			{ 850, "D+" },
			{ 775, "C-" },
			{ 700, "C" },
			{ 625, "C+" },
			{ 550, "B-" },
			{ 500, "B" },
			{ 450, "B+" },
			{ 400, "A-" },
			{ 350, "A" },
			{ 300, "A+" },
			{ 250, "S-" },
			{ 210, "S" },
			{ 170, "S+" },
			{ 130, "SS" },
			{ 105, "U" },
			{ 80, "X" },
		};
		var expectedGrade = "F";
		if (curResultShown.isSuccessful)
			foreach (var aTimeGrade in timeGrades)
			{
				if (curResultShown.timeTaken < aTimeGrade.Key)
					expectedGrade = aTimeGrade.Value;
			}
		centerMesh.text = expectedGrade;
		directionsText[1].text = curResultHighlightIdx + 1 < allResults.Count ? directionSymbols[1] : "";
		directionsText[3].text = curResultHighlightIdx > 0 ? directionSymbols[3] : "";
		directionsText[2].text = "";
		directionsText[0].text = curResultShown.questionsAnswered >= 10 ? "T" : "";
		if (showingPassword)
		{
			var sum = 0;
            for (var x = 0; x < curResultShown.expectedAnswer.Length; x++)
            {
				sum *= 3;
				sum += curResultShown.expectedAnswer[x];
			}
			timerMesh.text = sum.ToString("00");
			statusMesh.text = curResultShown.submittedAnswer.Select(a => a < 0 ? "-" : a.ToString()).Join("");
			statusMesh.color = curResultShown.isSuccessful ? Color.green : Color.red;
		}
		else
		{
			statusMesh.text = curResultShown.questionsAnswered.ToString("00") + "/10";
			timerMesh.text = curResultShown.timeTaken.ToString("00");
			statusMesh.color = Color.white;
		}
	}
	void StartSubmission()
	{
		QuickLog("Time to submit a passcode.");
		centerMesh.text = "";
		statusMesh.text = "----";
		for (var x = 0; x < directionInputDigits.Length; x++)
		{
			directionsText[x].text = selectedCharacters.Substring(directionInputDigits[x], 1);
		}
		tpMessagePrompt = string.Format("Prompt is empty. Showing the following: {0}", Enumerable.Range(0, 4).Select(a => "[" + debugDirections[a].First() + ": " + selectedCharacters[a] + "]").Join(" "));
	}

	void HandleHighlightAnswer(int idx = -1)
	{
		for (var x = 0; x < directionsText.Length; x++)
			directionsText[x].text = idx == x ? directionSymbols[x] : lastArrowDisplayedTexts[x];
	}
	void StartAttempt()
	{
		lastHighlightedIdx = -1;
		showingPassword = false;
		isAnimating = true;
		timeTicker = CountTimeUp();
		StartCoroutine(timeTicker);
	}
	private IEnumerator CountTimeUp()
	{
		mAudio.PlaySoundAtTransform("Startup", transform);
		for (var x = 0; x < directionsText.Length; x++)
			directionsText[x].text = "";
        
		statusMesh.text = "";
		statusMesh.color = Color.white;
		centerMesh.text = "";
		for (float x = 0f; x < 1f; x += Time.deltaTime)
		{
			timerMesh.text = Mathf.FloorToInt(999 * (1f - x)).ToString("00");
			yield return null;
		}
		isAnimating = false;
		timeTaken = 0;
		curQuestionIdx = 0;
		CreateCurQuestion();
		hasStarted = true;
		while (timeTaken < 999)
		{
			yield return null;
			timeTaken += Time.deltaTime;
			timerMesh.text = timeTaken.ToString("00");
		}
		timeTaken = 999;
		timerMesh.text = "999";
	}
	public class Result	
	{
		public bool isSuccessful;
		public float timeTaken;
		public int questionsAnswered;
		public int[] expectedAnswer, submittedAnswer;
		public Result(bool success, float t, int q, int[] eA, int[] sA)
        {
			expectedAnswer = eA;
			submittedAnswer = sA;
			timeTaken = t;
			isSuccessful = success;
			questionsAnswered = q;
        }
	}

	IEnumerator TwitchHandleForcedSolve()
    {
		if (!hasStarted)
			directionsSelectable[2].OnInteract();
		do
			yield return true;
		while (isAnimating);
		for (var x = curQuestionIdx; x < generatedDirectionIdxes.Length; x++)
        {
			directionsSelectable[generatedDirectionIdxes[x]].OnInteract();
			//yield return true;
			yield return new WaitForSeconds(0.1f);
        }
		while (!currentSubmissionIdx.SequenceEqual(expectedSubmissionIdx))
        {
			for (var x = 0; x < expectedSubmissionIdx.Length; x++)
            {
				var idxDirDigitCur = Enumerable.Range(0, 4).Single(a => directionInputDigits[a] == expectedSubmissionIdx[x]);
				directionsSelectable[idxDirDigitCur].OnInteract();
				yield return new WaitForSeconds(0.1f);
			}
        }
		var idxsubDir = Enumerable.Range(0, 4).Single(a => directionInputDigits[a] == 3);
		directionsSelectable[idxsubDir].OnInteract();
		//yield return new WaitForSeconds(0.1f);
	}

#pragma warning disable 414
	private readonly string BaseTwitchHelpMessage = "\"!{0} U/L/R/D\" [Presses directional button. Presses can be chained when entering the passcode.] \"!{0} prompt enable/disable/on/off/toggle\" [Enables/disables/toggles sending a message to chat for the current prompt.]";
	private string TwitchHelpMessage = "\"!{0} U/L/R/D\" [Presses directional button. Presses can be chained when entering the passcode.] \"!{0} prompt enable/disable/on/off/toggle\" [Enables/disables/toggles sending a message to chat for the current prompt.]";
#pragma warning restore 414

	private IEnumerator ProcessTwitchCommand(string command)
	{
		var matchPrompt = Regex.Match(command, @"^prompts?\s(on|off|toggle|enable|disable)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (matchPrompt.Success)
        {
			var lastPortions = matchPrompt.Value.Split().Skip(1);
			var portionSetLast = lastPortions.Single().ToUpperInvariant();
			switch (portionSetLast)
            {
				case "ON":
				case "ENABLE":
					{
						allowTPSayPrompt = true;
						yield return "sendtochat Chat Prompts have been enabled. Check the chat when you start a new attempt.";
						break;
					}
				case "OFF":
				case "DISABLE":
					{
						allowTPSayPrompt = false;
						yield return "sendtochat Chat Prompts have been disabled.";
						break;
					}
				case "TOGGLE":
                    {
						allowTPSayPrompt ^= true;
                        yield return "sendtochat Chat Prompts have been toggled" + (allowTPSayPrompt ? "on" : "off") + ".";
						break;
					}
				default:
                    {
						yield return "sendtochaterror Unknown prompt command \"" + portionSetLast + "\"";
						break;
                    }
            }
			TwitchHelpMessage = BaseTwitchHelpMessage + (allowTPSayPrompt ? " The module will send a message regarding the current prompt being enabled." : "");
			yield break;
        }
		var commandModified = command.ToUpperInvariant().Replace(" ", "");
		if (commandModified.Any(x => !"URDL".Contains(x.ToString())))
		{
			yield return "sendtochaterror Only U, L, R, and D or valid commands.";
			yield break;
		}
		if (commandModified.Length > 1 && curQuestionIdx <= 9)
		{
			yield return "sendtochaterror Only one answer to a prompt may be sent at a time.";
			yield break;
		}
		if (curQuestionIdx > 9)
		{
			int[] p = commandModified.Select(x => "URDL".IndexOf(x.ToString())).ToArray();
			for (int i = 0; i < p.Length; i++)
			{
				yield return null;
				directionsSelectable[p[i]].OnInteract();
				if (directionInputDigits[p[i]] == 3) yield break;
			}
		}
		else
		{
			yield return null;
			while (isAnimating)
				yield return "trycancel";
			directionsSelectable["URDL".IndexOf(commandModified)].OnInteract();
			if (isAnimating)
				while (!hasStarted)
					yield return "trycancel";
			
			yield return null;
			if (hasStarted && allowTPSayPrompt)
				yield return "sendtochat " + tpMessagePrompt ?? "";
			
		}
	}
}
