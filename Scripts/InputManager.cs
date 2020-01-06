using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// </summary>
// the only instance of every-frame input reading.
// other classes should take off of this
public class InputManager : MonoBehaviour
{
	public SpriteRenderer crosshairSprite;
	public Texture2D cursorTexture;
	HUDScripts hud;
	
	public Joystick joyStick;
	public Joybutton joyButton;
	Camera cam;
	
	static KeyCode A_BUTTON = KeyCode.JoystickButton0;
	
	Vector3 aimDirVec;
	PlayerController player;
	
	int controlMethod = 0;
	const int CONTR_MOUSE = 0;
	const int CONTR_CONTROLLER = 1;
	const int CONTR_JOY = 2; // joystick, aka mobile
	bool demoMode = false;
	
	bool buttonDown = false;
	bool buttonHeld = false;
	bool buttonUp = false;
	Vector2 targetPosition;
	float analogControlMinimum = 0.15f; // prevent micromovement being registered as input

	void Start(){
		player = GameManager.GM.player.GetComponent<PlayerController>();
		hud = HUDScripts.Instance;
		cam = Camera.main; // this is actually a search-by-tag function
		
		Cursor.visible = false; // computer cursor false; crosshair tracks as a sprite when on mouse mode
		
		#if UNITY_IOS
			SetControlsToMobile();
		#elif UNITY_ANDROID
			SetControlsToMobile();
		#else
			hud.SetDesignToPC(); // design defaults to mobile.
		#endif
		
		// for testing other UI configs
		//SetControlsToMobile(); 
		//SetControlsToDemo();
	}
	
	void SetControlsToMobile(){
		controlMethod = CONTR_JOY;
		hud.SetDesignToMobile();
		joyButton.onDown.AddListener(JoybuttonListener);
		joyButton.onUp.AddListener(JoybuttonListener);
	}
	
	void SetControlsToDemo(){
		demoMode = true;
		controlMethod = CONTR_CONTROLLER;
		hud.SetDesignToMobile();
		Debug.Log("DEMO MODE ACTIVE");
	}

	// update is per frame
	void Update(){
		if (!demoMode){
			CheckInputMethodChange();
		}
		// demo mode. takes controller inputs and imitates mobile ui inputs
		else{
			float horiz = Input.GetAxis("HorizontalController");
			float verti = Input.GetAxis("VerticalController");
			Vector2 controlDirVec = Vector2.zero;
			if (!(horiz == 0f && verti == 0f)){
				controlDirVec = new Vector2(horiz, verti);
				if (controlDirVec.magnitude < analogControlMinimum){
					controlDirVec = Vector2.zero;
				}
				controlDirVec.Normalize();
			}

			joyStick.UpdateHandle(controlDirVec);
			if (buttonHeld){
				joyButton.PressedImage(true);
			}
			else{
				joyButton.PressedImage(false);
			}
		}
	}
	
	// cursor in mouse mode jittering fixed by having these in lateupdate
	void LateUpdate(){
		RecordInputs();
		UpdateTargetPosition();
	}
	
	// check for either mouse or controller inputs
	void CheckInputMethodChange(){
		if (controlMethod != CONTR_MOUSE && CheckMouseInput()){
			controlMethod = CONTR_MOUSE;
		}
		else if (controlMethod != CONTR_CONTROLLER && CheckContrInput()){
			controlMethod = CONTR_CONTROLLER;
		}
	}
	
	bool CheckMouseInput(){
		return (Input.GetAxis("Mouse X") != 0);
	}
	 
	bool CheckContrInput(){
		// https://forum.unity.com/threads/check-whether-controller-or-keyboard-is-being-used-via-script.359608/ - extensive keyboard-or-controller testing based on currently mapped buttons. overkill for minimal buttons as is here.
		
		// the HorizontalController axis only receives controller input
		float horiz = Input.GetAxis("HorizontalController");
		float input = Input.GetAxis("Fire1Controller");
		if (horiz != 0f || input != 0f){
			return true;
		}
		
		return false;
	}
	
	void RecordInputs(){
		if (controlMethod == CONTR_MOUSE){ // mouse
			buttonDown = Input.GetMouseButtonDown(0);
			buttonHeld = Input.GetMouseButton(0);
			buttonUp = Input.GetMouseButtonUp(0);
			targetPosition = (Vector2)cam.ScreenToWorldPoint(Input.mousePosition);
		}
		else if (controlMethod == CONTR_CONTROLLER){ // contr, analog
			buttonDown = Input.GetKeyDown(A_BUTTON);
			buttonHeld = Input.GetKey(A_BUTTON);
			buttonUp = Input.GetKeyUp(A_BUTTON);
			targetPosition = CalcCrosshairPosAsPlayerOffset();
		}
		else if (controlMethod == CONTR_JOY){ // joy (mobile)
			// a bit special due to canvas button listener
			targetPosition = CalcCrosshairPosAsPlayerOffset();
		}
	}
	
	// unityevent receiver
	void JoybuttonListener(){
		buttonDown = joyButton.pressed;
		buttonHeld = buttonDown;
		buttonUp = !buttonDown;
	}
	
	void UpdateTargetPosition(){
		transform.position = targetPosition;
	}
	
	// calculates crosshair's position to be close to the player, on a short radius offset.
	// this is for controller/analog aiming.
	Vector2 controlDirVec = new Vector2(0, 1); // remember last input
	Vector3 CalcCrosshairPosAsPlayerOffset(float distance = 1f){
		// get input
		float horiz;
		float verti;
		if (controlMethod == CONTR_CONTROLLER){
			horiz = Input.GetAxis("HorizontalController");
			verti = Input.GetAxis("VerticalController");
		}
		else{ // else mobile
			horiz = joyStick.Horizontal;
			verti = joyStick.Vertical;
		}
		if (!(horiz == 0f && verti == 0f)){
			if ((new Vector2(horiz, verti)).magnitude < analogControlMinimum){
				// if input is nothing, the offset should still be the same (retain offset and direction)
				// controlDirVec = Vector2.zero; // commented out for remembering last input instead
			}
			else{
				controlDirVec = new Vector2(horiz, verti);
				controlDirVec.Normalize();
			}
		}
		
		var x = player.transform.position.x + (distance * controlDirVec.x);
        var y = player.transform.position.y + (distance * controlDirVec.y);

        return new Vector3(x, y, 0);
	}
	
	// calculates aim direction based on where the crosshair currently is.
	// called by playercontroller getting updates on where to chargemovement, and by hook for firing direction.
	public Vector3 GetAimDirVec(){
		aimDirVec = transform.position - player.transform.position;
		aimDirVec.Normalize();
		return aimDirVec;
	}
	
	public bool GetButtonDown(){
		return buttonDown;
	}
	
	public bool GetButtonHeld(){
		return buttonHeld;
	}
	
	public bool GetButtonUp(){
		return buttonUp;
	}
}
