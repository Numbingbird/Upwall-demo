using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;	// system also has a 'random' definition

// usage:
// ScoreScripts.Instance.AddBone(1);

// keeps track of both combo and score (currency); updates display accordingly
// score = bones (currency)
// how combos work currently:
// flat is 1x multi, but displays 0
// build up to 4 for fever mode
public class ScoreScripts : MonoBehaviour
{
	// instanced for scorekeeping called by in-scene objects
	public static ScoreScripts Instance;
	void Awake() {
		Instance = this;
	}
	
	public ShadowedTextChanger bonesText;
	public ShadowedTextChanger healthText;
	public GameObject healthBar;
	public RectTransform comboTextMasker; // used for mask reduce-size effect
	public RectTransform comboTextParent; // used for position manip scrolling effect
	public ShadowedTextChanger comboText;
	public Animation comboTextAnim; // for the pop-out effect
	List<KeyValuePair<string, double>> comboTextOptions;
	double comboTextWeightTotal = 0.0;
		
	// score
	int bonesCurrent = 0;
	int bonesCollectedThisRun = 0;
	
	// multiplier
	const int multiplierBase = 0;
	const int multiplierMax = 4;
	int multiplier = multiplierBase;
	
	// timer
	float timerCurrent = 0;
	float timerMax = 4f;
	const float timerExtend = 2.8f;
	bool timerActive = false;
	
	// combo text vertical masking
	float comboMaskVertSizeInit;
	
	// combo text scrolling
	float comboTextScrollSpeed = 0.6f;
	const float comboTextScrollOSize = 18f; // leftshift adjust, should be horizontal size of one "O"
	const string comboTextScrollStr = "AWOOOOOOO";
	const string comboTextScrollExtended = "OOOOOOOOO"; // this relies on equal kernelling between A, W, O
	bool comboTextScrollFirstLoop = true;
	const float comboTextScrollFirstLoopCond = -22f;
	Vector2 comboTextParentInitPos;
	
	// other
	int comboActual = 0;
	int comboHighestThisRun = 0;
	
	void Start(){
		comboTextOptions = new List<KeyValuePair<string, double>>{
			new KeyValuePair<string, double>("BARK", 1),
			new KeyValuePair<string, double>("WOOF", 1),
			new KeyValuePair<string, double>("YIP", 0.3),
			new KeyValuePair<string, double>("RUFF", 0.7),
			new KeyValuePair<string, double>("BOW", 0.3),
			new KeyValuePair<string, double>("WOW", 0.3),
			new KeyValuePair<string, double>("ARF", 0.7),
			new KeyValuePair<string, double>("BAU", 0.05),
			new KeyValuePair<string, double>("WAN", 0.05),
			new KeyValuePair<string, double>("HAU", 0.05),
			new KeyValuePair<string, double>("HAV", 0.05),
			new KeyValuePair<string, double>("WUFF", 0.3)
		};
		for (int i = 0; i < comboTextOptions.Count; i++)
			comboTextWeightTotal += comboTextOptions[i].Value;
		
		
		comboMaskVertSizeInit = comboTextMasker.rect.height;
		comboTextParentInitPos = comboTextParent.localPosition;
		
		Invoke("UpdateUIMultiplier", 0.3f);
	}
	
	// ===== animation
	// =====
	
	string ComboTextRoll(){
		float diceRoll = Random.Range(0, (float)comboTextWeightTotal);
		double cumulative = 0.0;
		for (int i = 0; i < comboTextOptions.Count; i++)
		{
			cumulative += comboTextOptions[i].Value;
			if (diceRoll < cumulative)
			{
				return comboTextOptions[i].Key;
			}
		}
		return comboTextOptions[comboTextOptions.Count - 1].Key;
	}
	
	// ===== outside influence
	// =====
	
	void Update(){
		TimerTick();
	}
	
	void FixedUpdate(){
		UpdateUIMultiplierAtMax();
	}
	
	// getting bones don't increase multi, just extends combo if any
	public void AddBone(int add){
		if (timerActive){
			TimerExtend();
		}
		int addActual = add;
		if (multiplier == 0) addActual = add;
		else addActual = multiplier * add;
		bonesCurrent += addActual;
		bonesCollectedThisRun += addActual;
		UpdateUIBones();
	}
	
	public void EnemySlain(){
		ComboAdd();
	}
	
	// ===== combo system
	// =====
	
	void ComboAdd(){
		if (!timerActive){
			ComboBegin();
			ComboIncrease();
		}
		else{
			ComboIncrease();
			TimerExtend();
		}
		comboActual += 1;
	}
	
	void ComboBegin(){
		timerCurrent = timerMax;
		timerActive = true;
	}
	
	void ComboIncrease(){
		if (multiplier < multiplierMax){
			multiplier += 1;
			UpdateUIMultiplier();
		}
	}
	
	void ComboEnd(){
		timerActive = false;
		timerCurrent = 0;
		UpdateUITimer();
		
		multiplier = multiplierBase;
		UpdateUIMultiplier();
		
		if (comboActual > comboHighestThisRun){
			comboHighestThisRun = comboActual;
		}
		comboActual = 0;
	}
	
	// ===== timer system
	// =====
	
	void TimerTick(){
		if (timerActive){
			timerCurrent -= Time.deltaTime;
			UpdateUITimer();
		}
		// end combo
		if (timerActive && timerCurrent <= 0){
			ComboEnd();
		}
	}
	
	void TimerExtend(){
		timerCurrent += timerExtend;
		if (timerCurrent > timerMax){
			timerCurrent = timerMax;
		}
		UpdateUITimer();
	}

	// ===== ui
	// =====
	
	void UpdateUITimer(){
		// https://docs.unity3d.com/Manual/script-Mask.html
		// use mask on text parent, vertically shrink to 0, scale by time remaining
		comboTextMasker.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, (timerCurrent / timerMax) * comboMaskVertSizeInit);
	}
	
	void UpdateUIMultiplier(){
		/* dog noise text*/
		if (multiplier == 0){
			comboText.TextChange("");
			comboTextParent.localPosition = comboTextParentInitPos; // reset scroll offset
		}
		else if (multiplier == multiplierMax){
			comboText.TextChange(comboTextScrollStr);
			comboTextScrollFirstLoop = true;
		}
		else{
			comboText.TextChange(ComboTextRoll());
			// add some other visual flavor to confirm combo increment
			// play bounce animation - mimics patapon
			comboTextAnim.Play();
		}
	}
	
	// called by fixedupdate 
	// the scrolling effect comes from this
	void UpdateUIMultiplierAtMax(){
		if (multiplier == multiplierMax){
			comboTextParent.localPosition = new Vector2(comboTextParent.localPosition.x - comboTextScrollSpeed, comboTextParent.localPosition.y);
			// for whatever reason the mask just makes text dissapears at -50
			// whenever it passes a magic number, shunt it back one O worth of distance
			if (comboTextParent.localPosition.x < comboTextScrollFirstLoopCond){
				comboTextParent.localPosition = new Vector2(comboTextParent.localPosition.x + comboTextScrollOSize, comboTextParent.localPosition.y);
				// on first loop, wait for AW to leave the screen, then change the whole string to Os. this completes the looping effect - since the space taken by W is now an O.
				if (comboTextScrollFirstLoop){
					comboText.TextChange(comboTextScrollExtended);
					comboTextScrollFirstLoop = false;
				}
			}
		}
	}
	
	void UpdateUIBones(){
		bonesText.TextChange(bonesCurrent.ToString());
	}
	
	public void UpdateHealth(int newHealth, int healthMax = 4){
		healthText.TextChange(newHealth.ToString() + "/" + healthMax.ToString());
		healthBar.transform.localScale = new Vector3((float)newHealth / (float)healthMax, 1, 1);
	}
}
