using System;	// needed for List<Action>
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;	// system also has a random definition
using Cinemachine;

//https://blogs.unity3d.com/2018/05/29/procedural-patterns-you-can-use-with-tilemaps-part-i/
//https://gamedev.stackexchange.com/questions/150917/how-to-get-all-tiles-from-a-tilemap
public class MapScripts : MonoBehaviour
{
	// triggers; only usage in this script should be placement
	// 	triggers do their own things (self contained) or otherwise send messages back to mapscripts
	public TriggerBase StartGeneration;
	public TriggerBase GivePlayerControl;
	public TriggerBase EndLevel;
	TransitionEffect transition;
	
	// other refs
	BackgroundScripts background; // restricts cinemachine
	GameObject hurtbox; // currently unused, softlock-prevention hurtbox
	public CinemachineVirtualCamera cinemachine; // reference to dig into cinecomposer
	CinemachineFramingTransposer cineframing; // for snap-to-centre after player placement
	PlayerController player; // player reference moves the player to starting point
	public GameObject enemyTracker; // parent gameobject to put instantiated enemies under, for later deletion
	
	// tilemap related
	public RuleTile treeRuleTile;
    public Tilemap solidTilemap;
	int[,] solidMap; // [width, height]
		
	public Tile onewayTile;
	public Tilemap onewayTilemap;
	int[,] onewayMap;
	
	int[,] enemyMap;	
	
	List<int[,]> groundMaps; // reference list to iterate through maps pre-drawing. currently not used for anything.
	List<int[,]> dontSpawnMaps; // maps that enemies should not spawn inside. currently "iterates" over just solidmap
	
	int totalExpectedCoroutines = 4;
	int coroutinesFinished = 0;
	const int mapWidth = 19;
	int mapHeight = 250; // approx. 220~240 is good for actual level
	const int wallWidth = 3;
	const int heightUnder = 8; // how much height under the spawn zone should be placed to give illusion of solid tall tree
	int heightPointerTotal = 0;
	
	public List<GameObject> EnemyListZone1 = new List<GameObject>();

	Vector2 startPos = new Vector2(9, -60);
	Vector2 testbedPos = new Vector2(9, -70);
	
	void Awake(){
		// init
		InitMaps();
		
		// lists of maps. currently unused.
		groundMaps = new List<int[,]>{solidMap, onewayMap};
		dontSpawnMaps = new List<int[,]>{solidMap};
	}
	
	// called by GenerateLevel to refresh maps
	void InitMaps(){
		solidMap = new int[mapWidth, mapHeight];
		onewayMap = new int[mapWidth, mapHeight];
		enemyMap = new int[mapWidth, mapHeight];
		for (int i=0;i<mapWidth*mapHeight;i++) enemyMap[i%mapWidth,i/mapWidth] = -1;	// init as -1 for "spawn nothing"
	}
	
	void Start(){
		player = GameManager.GM.player.GetComponent<PlayerController>();
		background = GameManager.GM.background.GetComponent<BackgroundScripts>();
		hurtbox = GameManager.GM.hurtbox;
		cineframing = cinemachine.GetCinemachineComponent<CinemachineFramingTransposer>();
		transition = TransitionEffect.Instance;
	}
	
	// Update is called once per frame
		// temp while i set up the actual intro

	void Update ()
	{
		/*
		if (Input.GetKeyDown (KeyCode.T))
		{
			GenerateLevel();
		}
		if (Input.GetKeyDown (KeyCode.Y))
		{
			PlaceAndPushPlayer();
		}
		*/
	}
	
	//===== map generation

	public void GenerateLevel()
	{
		heightPointerTotal = 0;
		InitMaps(); // refresh maps
		GenerateMapWalls(solidMap);
		
		// set triggers
		// some initial height so player can come in from under
		heightPointerTotal += heightUnder;
		SetTriggerAtHeight(GivePlayerControl, heightPointerTotal + 1);
		SetTriggerAtHeight(EndLevel, mapHeight - 1);
		
		// start-of-level
		GenerateObstacleHorizontalLine(onewayMap, heightPointerTotal);
		heightPointerTotal += 7; // some breathing room
		
		// the actual level content
		GenerateObstacles();
		
		// oneway at the end
		GenerateObstacleHorizontalLine(onewayMap, mapHeight - 2, generateEnemies: false);
		
		// set hurtbox centered under player. auto height adjust is done by hurtboxscripts
		hurtbox.transform.position = new Vector3(mapWidth / 2, -5, 0);
		hurtbox.SetActive(true);
		
		totalExpectedCoroutines = 4; // how many corouts below
		coroutinesFinished = 0;
		StartCoroutine(IEWriteToMap(solidMap, solidTilemap, treeRuleTile));
		StartCoroutine(IEWriteToMap(onewayMap, onewayTilemap, onewayTile));
		StartCoroutine(IEChiselMap(solidMap, solidTilemap));
		StartCoroutine(IEChiselMap(onewayMap, onewayTilemap));
	}

	// called by the last coroutine to finish
	void OnCoroutinesFinished(){
		WriteEnemies(enemyMap, EnemyListZone1);
		SetCinemachineBoundaryObjectToGameArea();
		
		// temp - some character select ui thing should be calling this in the future, instead of calling after level is loaded
		PlaceAndPushPlayer();
	}
	
	void PlaceAndPushPlayer(){
		player.SetControlByWorld(true);
		player.SetPos(CalcPlayerStartPos());	// + 1 to give some breathing height
		player.SetVel(CalcPlayerStartVel());
		CameraCenterOnPlayer();
	}
	
	void CameraCenterOnPlayer(){
		cineframing.m_DeadZoneWidth = 0f;
		cineframing.m_SoftZoneWidth = 0f;
		Invoke("CameraReleaseConstraint", 0.1f); // delayed call to let cinemachine center
	}

	void CameraReleaseConstraint(){
		cineframing.m_SoftZoneWidth = 1f;
		cineframing.m_DeadZoneWidth = 1f;
	}
	
	// this is different depending on widths
	public Vector2 CalcPlayerStartPos(){
		float initHeight = -1f;
		return (new Vector2(mapWidth/2, initHeight));	// + 1 to give some breathing height
	}
	
	public Vector2 CalcPlayerStartVel(){
		float initSpeed = 15f;
		return Vector2.up * initSpeed; // depends a little on start layout
	}
	
	// write walls to the map. includes floor.
	void GenerateMapWalls(int[,] map){
		// left wall
		for (int i = 0; i < wallWidth; i++){
		for (int j = 0; j < map.GetLength(1); j++){
            map[i,j] = 1;
		}
		}
		
		// right wall
		for (int i = map.GetLength(0) - wallWidth - 1; i < map.GetLength(0); i++){
		for (int j = 0; j < map.GetLength(1); j++){
            map[i,j] = 1;
		}
		}
		
		// floor:
		// currently not using floor, using oneway, generated as part of level center
	}
	
	// choose at what height level something should be generated.
	// generation goes:
	// units
	// 	subunits (if any)
	// sub-subunits are helpers like "small oneway platform"
	void GenerateObstacles(){
		int minHeightBetween = 6;
		int maxHeightBetween = 8;
		int leewaySpace = 6; // do-not generate this many tiles away from the end
		
		// possible decisions for level gen. this step splits the decision into generating from subunits and generating from... larger subunits, idk. probably don't need this step here, later on.
		List<Action> levelGenFunctions = new List<Action>(); 
		levelGenFunctions.Add(() => GenerateObstacleFromSubunits());
		
		for (
		;	// height start calculated elsewhere
		heightPointerTotal < mapHeight - leewaySpace;		// stop the loop at map height-leewaySpace so generation doesn't go over
		heightPointerTotal += Random.Range(minHeightBetween, maxHeightBetween)){
			//GenerateObstacleAtHeightLevel
			((Action)RandomElementInList(levelGenFunctions))();
		}
	}

	// ==== units. big blocks of terrain obstacles.
	// units need to define their own spawn locations to discourage bad spawns
		
	// build a horizontal line. obviously should not be given the solid tilemap (would softlock).
	void GenerateObstacleHorizontalLine(int[,] map, int heightPointer, bool generateEnemies = false){
		for (int i = wallWidth - 1; i < mapWidth - wallWidth; i++){
			map[i, heightPointer] = 1;
		}
		if (generateEnemies)
		GenerateEnemyNearLocation(new Vector2(mapWidth/2, heightPointer+2));
		heightPointerTotal += 4; // add cost
	}

	// usage of subunits.
	void GenerateObstacleFromSubunits(){
		bool flip = false;
		
		// add available subunits
		List<Action> levelGenFunctions = new List<Action>();
		levelGenFunctions.Add(() => GenerateObstacleRowMultiple(onewayMap, heightPointerTotal, flip));
		levelGenFunctions.Add(() => GenerateObstacleBranchSmall(solidMap, heightPointerTotal, flip));	// having two of these together, a bunch of times in a row, is not very fun for the default fasthook
		levelGenFunctions.Add(() => GenerateObstacleReachingBlock(heightPointerTotal, flip));
		
		// generate left side, remove the choice for second side
		int firstChoice = RandomIntInList(levelGenFunctions);
		((Action)levelGenFunctions[firstChoice])();
		levelGenFunctions.RemoveAt(firstChoice);
		
		// the second branch is slightly higher/lower.
		heightPointerTotal += Random.Range(-1, 2);
		flip = true;
		
		// generate right side
		((Action)RandomElementInList(levelGenFunctions))();
		
		// generate 1-2 enemies, both underneath the subunits
		// -6 because heightpointertotal was 
		if (Coinflip(0.7f))
		GenerateEnemyNearLocation(new Vector2(wallWidth + 1, heightPointerTotal-3));
		if (Coinflip(0.7f))
		GenerateEnemyNearLocation(new Vector2(mapWidth - wallWidth - 2, heightPointerTotal-3));
		
		// the average height cost of each subunit is around 4-5.
		heightPointerTotal += 4;
	}
	
	// subunits. mix and match handled by subunit combiner.
	// certain combinations should not be allowed (long branch + long branch).
	
	// build a block-protrusion.
	// the branch condition disables the building-back portion of the function, making it a thin branch.
	void GenerateObstacleBranchSmall(int[,] map, int heightPointer, bool flip = false){
		float buildEnergy = 100f;
		float minEnergyStep = 18f;
		float maxEnergyStep = 38f;
		float buildDirWeight = .3f;
		int widthPointer = wallWidth;
		bool thickBranch = Coinflip(0.8f);
		
		while (buildEnergy > 0f){
			TilePlace(map, heightPointer, widthPointer, flip);
			
			if (thickBranch){
				// build back towards the wall, making a solid protrusion
				int buildTowardsWall = widthPointer - 1;
				while (!TileQuery(map, heightPointer, buildTowardsWall, flip)){
					TilePlace(map, heightPointer, buildTowardsWall, flip);
					buildTowardsWall--;
				}
			}
			
			// lower buildDirWeight is height, higher is width.
			if (Random.value < buildDirWeight){
				widthPointer++;
			}
			else{
				heightPointer += 1;
			}
			buildEnergy -= Random.Range(minEnergyStep, maxEnergyStep);
		}
		
	}
	
	// looks like:
	/*
	---xxx
	   xxx
	    xx
	*/
	// doesn't make sense with passed map, so this one is done straight-up
	void GenerateObstacleReachingBlock(int heightPointer, bool flip){
		bool coinflip = Coinflip();
		int width = Random.Range(2, 6);
		int widthPointer = wallWidth;
		int widthPointerSecond = wallWidth;
		
		if (coinflip){
			widthPointer += width;
		}
		else{
			widthPointerSecond += 3; // width of hangingblock
		}
		
		GenerateObstacleHangingBlock(solidMap, heightPointer - 2, widthPointer, flip); // start from 2 under so the highest block matches up
		
		GenerateObstacleRow(onewayMap, heightPointer, widthPointerSecond, flip, width);
	}
	
	
	void GenerateObstacleRowMultiple(int[,] map, int heightPointer, bool flip){
		// start a little lower to make headspace for multiple platforms 
		// also guarantees at least one platform is lower
		//heightPointer -= Random.Range(2, 4);
		
		// 1-2 platforms. weighted towards 1.
		for (int i = 0; i < (int)(Math.Floor(Random.Range(1f,2.3f))); i++){
			int widthPointer = wallWidth + Random.Range(0, 5);
			int width = Random.Range(3, 6);
			
			GenerateObstacleRow(map, heightPointer, widthPointer, flip, width);
			
			// add 3-4 to height for next platform.
			heightPointer += Random.Range(3,5);
		}
	}
	
	// sample usage of horizontalLineSmall
	void GenerateObstacleRowRandom(int[,] map, int heightPointer, bool flip, int widthPointer = -1, int width = -1){
		if (widthPointer == -1){
			widthPointer = (int)(mapWidth / 2) - 3;
		}
		if (width == -1){
			width = Random.Range(3, 6);
		}
		GenerateObstacleRow(map, heightPointer, widthPointer, flip, width);
	}
	
	// sub-subunits
	// reusuable minipieces
	
	// Generate small platform.
	void GenerateObstacleRow(int[,] map, int heightPointer, int widthPointer, bool flip = false, int width = 3){
		while (width > 0){
			TilePlace(map, heightPointer, widthPointer, flip);
			widthPointer++;
			width--;
		}
	}
	
	// makes a 3x3, sometimes less near the bottom.
	void GenerateObstacleHangingBlock(int [,] map, int heightPointer, int widthPointer, bool flip){
		for (int h = heightPointer; h < heightPointer + 3; h++){
			for (int w = widthPointer; w < widthPointer + 3; w++){
				if (h == heightPointer && Coinflip()){
					TilePlace(map, h, w, flip);
				}
				else if (h != heightPointer){
					TilePlace(map, h, w, flip);
				}
			}
		}
	}
	
	// helpers that handle the flip stuff
	
	// function for placing any single tile on the map.
	// flip doesn't have to be coded in any of the other subgens.
	void TilePlace(int[,] map, int heightPointer, int widthPointer, bool flip = false){
		if (!flip){
			map[widthPointer, heightPointer] = 1;
		}
		else{
			map[mapWidth - widthPointer - 2, heightPointer] = 1;
		}
	}
	
	// similarly, checking for tile existence, also handling flipside
	bool TileQuery(int[,] map, int heightPointer, int widthPointer, bool flip = false){
		if (!flip){
			return (map[widthPointer, heightPointer] == 1);
		}
		else{
			return (map[mapWidth - widthPointer - 2, heightPointer] == 1);
		}
	}
	
	// ===== enemy generation. 
	
	// generate an enemy in the vicinity. check that it's not being put inside a wall.
	void GenerateEnemyNearLocation(Vector2 location){
		float variabilityX = 1; // +-1
		float variabilityY = 1;
		Vector2 enemyLocation;

		// attempts until deciding it's too occupied
		for (int attempts = 0; attempts < 5; attempts++){
			enemyLocation.x = Random.Range(location.x - variabilityX, location.x + variabilityX);
			enemyLocation.y = Random.Range(location.y - variabilityY, location.y + variabilityY);
			if (CheckSpawnLocationForEmpty(enemyLocation)){
				GenerateRandomEnemy(enemyLocation);
				return;
			}
		}
		Debug.Log("enemy spawn failed: nearby locations unavailable, " + location);
	}
	
	void GenerateRandomEnemy(Vector2 location){
		GenerateSpecificEnemy(location, RandomIntInList(EnemyListZone1));
	}
	
	void GenerateSpecificEnemy(Vector2 location, int enemyRef){
		enemyMap[(int)location.x, (int)location.y] = enemyRef;
	}
	
	// ===== 
	// big whole-map affecting functions

	//Clear the map
	void ClearMap(Tilemap tilemap){
		tilemap.ClearAllTiles();
	}

	//restore the bounds to the outmost tiles.
	// Just a cleanup and polish thing. Could be useful for bugfix, apparently.
	void CompressBounds(Tilemap tilemap){
		tilemap.CompressBounds();
	}

	//writes tiles to map. no deletion.
	// is a coroutine because settile lags.
	IEnumerator IEWriteToMap(int[,] map, Tilemap tilemap, TileBase tile){
		int x = map.GetUpperBound(0);
		int y = map.GetUpperBound(1);
		// yield every X tiles written. find a balance between dropped frames and waiting too long for level gen.
		int yieldCounter = 0;
		int yieldEveryX = 20;
		
		for (int i = 0; i < x * y; i++){
			// checking for already placed with GetTile is either faster or same-time, test confirmed.
			if (map[i%x,i/x] == 1 
			&& tilemap.GetTile(new Vector3Int(i%x, i/x, 0)) != tile){
				tilemap.SetTile(new Vector3Int(i%x, i/x, 0), tile);
				yieldCounter++;
			}
			if (yieldCounter == yieldEveryX){
				yieldCounter = 0;
				yield return null;
			}
		}
		CoroutFinishedCounter();
	}
	
	// removes bits of a map by setting null tiles
	IEnumerator IEChiselMap(int[,] map, Tilemap tilemap){
		int x = map.GetUpperBound(0);
		int y = map.GetUpperBound(1);
		int yieldCounter = 0;
		int yieldEveryX = 20;
		for (int i = 0; i < x * y; i++){
			if (map[i%x,i/x] == 0 
			&& tilemap.GetTile(new Vector3Int(i%x, i/x, 0)) != null){
				tilemap.SetTile(new Vector3Int(i%x, i/x, 0), null);
				yieldCounter++;
			}
			if (yieldCounter == yieldEveryX){
				yieldCounter = 0;
				yield return null;
			}
		}
		CoroutFinishedCounter();
	}
	
	void CoroutFinishedCounter(){
		coroutinesFinished++;
		if (coroutinesFinished == totalExpectedCoroutines){
			OnCoroutinesFinished();
		}
	}
	
	void WriteEnemies(int[,] map, List<GameObject> enemyList){
		int x = map.GetUpperBound(0);
		int y = map.GetUpperBound(1);
		Vector2 locationToSpawn = Vector2.zero;
		
		// loops through width/height, but fancier
		for (int i = 0; i < x * y; i++){
			if (map[i%x,i/x] != -1){	// enemies was init as -1
				// spawn squarely in the center of the tile rather than bottom left
				locationToSpawn = new Vector2(i%x + .5f, i/x + .5f);
				var enemy = Instantiate(enemyList[map[i%x,i/x]], locationToSpawn, Quaternion.identity);
				// linked to a tracker parent so they can be cleaned up later
				enemy.transform.parent = enemyTracker.transform;
			}
		}
	}
	
	bool CheckSpawnLocationForEmpty(Vector2 location){
		foreach (int[,] map in dontSpawnMaps){
			if (map[(int)location.x, (int)location.y] != 0){
				return false;
			}
		}
		return true;
	}
	
	// ===== triggers
	// =====
	
	void SetTriggerAtHeight(TriggerBase trigger, float height){
		Vector2 pos = new Vector2(mapWidth / 2, height + .5f); // +.5f because tile alignment vs object alignment
		Vector2 scale = new Vector2(mapWidth, 1);
		trigger.SetPos(pos);
		trigger.SetScale(scale);
	}
	
	// end level protocol
	public void TriggeredEndLevel(){
		transition.TransitionOut();
		StartCoroutine(OnTransitionOutFinished());
	}
	
	IEnumerator OnTransitionOutFinished(){
		// use coroutine to wait for the transition to finish.
		while(transition.playingTransitionOut){
			yield return null;
		}
		// enemy cleanup (destroy)
		foreach (GameObject enemy in enemyTracker.GetGameObjects()){
			Destroy(enemy);
		}
		player.SetPos(startPos);
		SetCinemachineBoundaryObjectToTestbed();
		// invoke delay is desired to let camera settle to new pos
		Invoke("TransitionInImmediate", 0.5f);
	}
	
	void TransitionInImmediate(){
		transition.TransitionInImmediate();
	}
	
	// ===== helpers
	// =====
	
	int RandomIntInList<T>(List<T> inputList){
		return Random.Range(0, inputList.Count);
	}
	
	object RandomElementInList<T>(List<T> inputList){
		return inputList[RandomIntInList(inputList)];
	}
	
	void SetCinemachineBoundaryObjectToGameArea(){
		background.transform.position = new Vector3(mapWidth/2, mapHeight/2, 0);
		background.transform.localScale = new Vector3(background.transform.localScale.x, mapHeight, 1);
		
		background.UpdateCollider();
	}
	
	void SetCinemachineBoundaryObjectToTestbed(){
		background.transform.position = new Vector3(mapWidth/2, -60, 0);
		background.transform.localScale = new Vector3(background.transform.localScale.x, mapHeight, 1);
		
		background.UpdateCollider();
	}
	
	bool Coinflip(float comp = 0.5f){
		return Random.value < comp;
	}
}
