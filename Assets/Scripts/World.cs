using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class World : MonoBehaviour { 


    public enum quantumConfig
{
    Zero = 0,
    One = 1,
    Two = 2,
    Three = 3,
    Four = 4,
}
    public quantumConfig quantumDropdown = quantumConfig.Zero;


    System.Random rng = new System.Random();

    public MapGenerator mapGenerator;
    public MeshGenerator meshGenerator;
    public WorldMeshGenerator worldMeshGenerator;
    public int cacheSize = 20;
    private int currentQuantumMesh = 0;
    public float conflictingExitCutoffDistance = 7f;

    private WorldPiece currentWorldPiece;
    public MeshFilter combinedWallsMeshFilter;
    public MeshFilter combinedCeilingMeshFilter;
    public MeshCollider meshCollider;
    public Player player;

    public Dictionary<int, WorldPiece> worldPieceCache;
    private Dictionary<int, List<Vector3>> currentWorldPieceExitPositions;
    private Dictionary<int, List<int>> cloneList = new Dictionary<int, List<int>>();
    private Dictionary<int, List<GameObject>> spawnedBeacons = new Dictionary<int, List<GameObject>>();
    private List<int> visibleExits;
    private GameObject canvas;
    private AudioSource audioSource;
    
    public Camera mainCamera;

    public bool godMode=false;

    void Start() {
        audioSource = GameObject.FindWithTag("Audio").GetComponent<AudioSource>();
        canvas = GameObject.FindWithTag("Instructions");
        currentWorldPieceExitPositions = new Dictionary<int, List<Vector3>>();
        FillWorldPieceCache();
        currentWorldPiece = worldPieceCache[rng.Next(worldPieceCache.Count)];
        currentWorldPieceExitPositions = currentWorldPiece.GetExitPerimeterPositions(transform.position);
        FillNetwork(currentWorldPiece);
        if (worldPieceCache.Count > 40) {
            Debug.Log("generated too many pieces");
            return;
        }
        GenerateQuantumMeshes();
        Debug.Log("<color=red>current world piece: </color>" + currentWorldPiece.ID);
        foreach (Exit exit in currentWorldPiece.ExitList.Values) {
            Debug.Log("Exit: " + exit.wallDir.ToString() + " world piece: " + currentWorldPiece.NavigationNetwork[exit.binaryConfig]);
        }
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.B)) {
            AddBeacon();
        }
        if (Input.GetKeyDown(KeyCode.Escape)){
            Application.Quit();
        }
        if (Input.GetKeyDown(KeyCode.N)) {
            if(godMode){
                SceneManager.LoadScene("DebugMode");
            } else {
                SceneManager.LoadScene("SampleScene");
            }
            
        }
        if (Input.GetKeyDown(KeyCode.H)) {
            canvas.SetActive(canvas.activeSelf ? false : true);
        }
        if (Input.GetKeyDown(KeyCode.M)) {
            audioSource.mute = audioSource.mute ? false : true;
        }
        if (Input.GetKeyDown(KeyCode.D)){
            if(godMode){
                SceneManager.LoadScene("SampleScene");
            } else {
                SceneManager.LoadScene("DebugMode");
            }           
        }
        if (player.CheckForPlayerExit(transform.position, currentWorldPiece, out int nextActivePieceBinary)) {
            SwitchCurrentWorldPiece(nextActivePieceBinary);
        }
        visibleExits = player.GetVisibleExitsList(currentWorldPieceExitPositions);
        currentQuantumMesh = GetCurrentQuantumMesh();
        ToggleBeacons();
        CombinedMeshesInstance currentMesh = currentWorldPiece.QuantumMeshConfigs[currentQuantumMesh];
        combinedCeilingMeshFilter.mesh = currentMesh.ceiling;
        combinedWallsMeshFilter.mesh = currentMesh.walls;
        meshCollider.sharedMesh = combinedWallsMeshFilter.mesh;
    }

    int GetCurrentQuantumMesh() {
        int binaryTotal = 0;
        foreach (int binaryValue in visibleExits)
            binaryTotal += binaryValue;
        foreach(KeyValuePair<WallDir, List<int>> entry in currentWorldPiece.ExitConflicts) {
            if(entry.Value.Count > 1) {
                if(visibleExits.Contains(entry.Value[0]) && visibleExits.Contains(entry.Value[1])){
                    int fartherExit = GetFartherExit(entry.Value[0], entry.Value[1], out float closerDistance);
                    if (closerDistance > conflictingExitCutoffDistance) {
                        binaryTotal -= (entry.Value[0] + entry.Value[1]);
                    } else {
                        binaryTotal -= fartherExit;
                    }
                    
                }
            }
        }
        return binaryTotal;
    }

    int GetFartherExit(int exitA, int exitB, out float closerDistance) {
        Vector3 aPos = currentWorldPiece.GetWorldPosOfCoords(transform.position, currentWorldPiece.ExitList[exitA].coords);
        Vector3 bPos = currentWorldPiece.GetWorldPosOfCoords(transform.position, currentWorldPiece.ExitList[exitB].coords);
        float aDistance = Vector3.Distance(player.transform.position, aPos);
        float bDistance = Vector3.Distance(player.transform.position, bPos);
        if (aDistance > bDistance) {
            closerDistance = bDistance;
            return exitA;
        }
        closerDistance = aDistance;
        return exitB;


    }

    void AddBeacon() {
        Vector3 beaconPos = player.transform.position - transform.position;
        beaconPos.z = 3.25f;
        currentWorldPiece.BeaconList.Add(beaconPos);
        Debug.Log("Adding beacon to clones of #" + currentWorldPiece.ID);
        if (cloneList.ContainsKey(currentWorldPiece.ID)) {
            foreach (int ID in cloneList[currentWorldPiece.ID]) {
                Debug.Log("added to clone #" + ID);
                worldPieceCache[ID].BeaconList.Add(beaconPos);
            }
        }
        SpawnBeacons();
        //GameObject beacon = Instantiate(Resources.Load("Beacon"), player.transform.position, Quaternion.identity) as GameObject;
        //if (spawnedBeacons.ContainsKey(0)) {
        //    spawnedBeacons[0].Add(beacon);
        //} else {
        //    spawnedBeacons.Add(0, new List<GameObject> { beacon });
        //}
    }

    void SpawnBeacons() {
        foreach (Vector3 beaconPos in currentWorldPiece.BeaconList) {
            GameObject beacon = Instantiate(Resources.Load("Beacon"), transform.position + beaconPos, Quaternion.identity) as GameObject;
            if (spawnedBeacons.ContainsKey(0)) {
                spawnedBeacons[0].Add(beacon);
            } else {
                spawnedBeacons.Add(0, new List<GameObject> { beacon });
            }
        }
        foreach (KeyValuePair<int, int> entry in currentWorldPiece.NavigationNetwork) {
            WorldPiece adjacentPiece = worldPieceCache[entry.Value];
            foreach(Vector3 beaconPos in adjacentPiece.BeaconList) {
                Debug.Log("found beacon in adjacent piece #" + adjacentPiece.ID);
                Vector3 adjustedBeaconPos = beaconPos + transform.position + currentWorldPiece.ExitConnectionOffsets[entry.Key];
                GameObject beacon = Instantiate(Resources.Load("Beacon"), adjustedBeaconPos, Quaternion.identity) as GameObject;
                if (spawnedBeacons.ContainsKey(entry.Key)) {
                    spawnedBeacons[entry.Key].Add(beacon);
                } else {
                    spawnedBeacons.Add(entry.Key, new List<GameObject> { beacon });
                }
;
            }
        }
    }

    void ToggleBeacons() {
        foreach (KeyValuePair<int, List<GameObject>> entry in spawnedBeacons) {
            if (!visibleExits.Contains(entry.Key) && entry.Key != 0) {
                foreach (GameObject beacon in entry.Value) {
                    beacon.SetActive(false);
                }

            } else {
                foreach (GameObject beacon in entry.Value) {
                    beacon.SetActive(true);
                }
            }
        }
    }

    void ClearBeacons() {
        foreach(List<GameObject> beaconList in spawnedBeacons.Values) {
            foreach(GameObject beacon in beaconList) {
                Destroy(beacon);
            }
        }
        spawnedBeacons.Clear();
    }


    void SwitchCurrentWorldPiece(int nextPieceIndex) {
        ClearBeacons();
        transform.position += currentWorldPiece.ExitConnectionOffsets[nextPieceIndex];
        currentWorldPiece = worldPieceCache[currentWorldPiece.NavigationNetwork[nextPieceIndex]];
        currentWorldPieceExitPositions = currentWorldPiece.GetExitPerimeterPositions(transform.position);
        SpawnBeacons();
        Debug.Log("<color=red>current world piece: </color>" + currentWorldPiece.ID);
        foreach (Exit exit in currentWorldPiece.ExitList.Values) {
            Debug.Log("Exit: " + exit.wallDir.ToString() + " world piece: " + currentWorldPiece.NavigationNetwork[exit.binaryConfig]);
        }
    }



    int networkFillAttempts = 0;

    void FillNetwork(WorldPiece worldPiece) {
        worldPiece.usedInMap = true;    
        networkFillAttempts++;
        if (networkFillAttempts > 1000) {
            Debug.Log("Recursive Network Fill Overload");
            return;
        }
        Queue<WorldPiece> WorldPiecesToPopulate = new Queue<WorldPiece>();
        int[] exitList = worldPiece.NavigationNetwork.Keys.ToArray();
        foreach (int exitBinary in exitList) {
            if (worldPiece.NavigationNetwork[exitBinary] == -1) {
                int[] cacheIDArray = GetRandomCacheIDArray();
                bool foundNextPiece = false;
                for (int i = 0; i < cacheIDArray.Length; i++) {
                    if (cacheIDArray[i] == worldPiece.ID)
                        continue;
                    WorldPiece nextWorldPiece = worldPieceCache[cacheIDArray[i]];
                    if (CheckWorldPieceExitCompatibility(worldPiece.ExitList[exitBinary], nextWorldPiece, out int compatibleExitBinary)) {
                        if (nextWorldPiece.NavigationNetwork[compatibleExitBinary] != -1) {
                            WorldPiece clonedWorldPiece = nextWorldPiece.GetClone(worldPieceCache.Count);
                            if (cloneList.ContainsKey(nextWorldPiece.ID)) {
                                cloneList.Add(clonedWorldPiece.ID, cloneList[nextWorldPiece.ID].ToList());
                                cloneList[clonedWorldPiece.ID].Add(nextWorldPiece.ID);
                                foreach (int clone in cloneList[nextWorldPiece.ID]) {
                                    cloneList[clone].Add(clonedWorldPiece.ID);
                                }
                                cloneList[nextWorldPiece.ID].Add(clonedWorldPiece.ID);
                            } else {
                                cloneList.Add(nextWorldPiece.ID, new List<int> { clonedWorldPiece.ID });
                                cloneList.Add(clonedWorldPiece.ID, new List<int> { nextWorldPiece.ID });
                            }
                         //   Debug.Log("piece #" + nextWorldPiece.ID + "cloned into piece #" + clonedWorldPiece.ID);
                            worldPieceCache.Add(clonedWorldPiece.ID, clonedWorldPiece);
                            nextWorldPiece = clonedWorldPiece;
                        }
                        ConnectWorldPieces(worldPiece, exitBinary, nextWorldPiece, compatibleExitBinary);
                        foundNextPiece = true;
                        WorldPiecesToPopulate.Enqueue(nextWorldPiece);
                        break;
                    }
                }
                if (!foundNextPiece) {
                    int generationAttempts = 0;
                    while (true) {
                        generationAttempts++;
                        if (generationAttempts > 100) {
                            Debug.Log("infinite generation loop while trying to find compatible world piece");
                            return;
                        }
                        WorldPiece nextWorldPiece = new WorldPiece(mapGenerator, meshGenerator, worldMeshGenerator, worldPieceCache.Count, !godMode);
                        if (CheckWorldPieceExitCompatibility(worldPiece.ExitList[exitBinary], nextWorldPiece, out int compatibleExitBinary)) {
                            worldPieceCache.Add(nextWorldPiece.ID, nextWorldPiece);
                            ConnectWorldPieces(worldPiece, exitBinary, nextWorldPiece, compatibleExitBinary);
                            WorldPiecesToPopulate.Enqueue(nextWorldPiece);
                            break;
                        }
                    }
                }
            }
        }
        while (WorldPiecesToPopulate.Count > 0)
            FillNetwork(WorldPiecesToPopulate.Dequeue());
    }

    void GenerateQuantumMeshes() {
        for(int i = 0; i < worldPieceCache.Count; i++) {
            WorldPiece piece = worldPieceCache[i];
            if (piece.usedInMap) {
                //Debug.Log("GENERATING MESHES FOR PIECE #" + piece.ID);
                //Debug.Log("navigation network: ");
                //foreach (int binaryConfig in piece.NavigationNetwork.Keys) {
                //    Debug.Log("Exit: " + binaryConfig + " world piece: " + piece.NavigationNetwork[binaryConfig]);
                //}
                piece.GenerateQuantumMesh(worldPieceCache);

            }
        }
    }

    void FillWorldPieceCache() {
        worldPieceCache = new Dictionary<int, WorldPiece>();
        for (var i = 0; i < cacheSize; i++) {
            WorldPiece newWorldPiece = new WorldPiece(mapGenerator, meshGenerator, worldMeshGenerator, i, !godMode);
            worldPieceCache.Add(newWorldPiece.ID, newWorldPiece);
        }
    }

    public void ConnectWorldPieces(WorldPiece worldPieceA, int exitBinaryA, WorldPiece worldPieceB, int exitBinaryB) {
        if (worldPieceA.NavigationNetwork[exitBinaryA] != -1)
            Debug.Log("overwrote connection in worldPiece #" + worldPieceA.ID);
        if(worldPieceB.NavigationNetwork[exitBinaryB] != -1)
            Debug.Log("overwrote connection in worldPiece #" + worldPieceB.ID);
        worldPieceA.NavigationNetwork[exitBinaryA] = worldPieceB.ID;
        worldPieceB.NavigationNetwork[exitBinaryB] = worldPieceA.ID;
        float exitPosX = (-(worldPieceA.PieceWidth / 2) + worldPieceA.ExitList[exitBinaryA].coords.tileX /*+ .5f*/);
        float exitPosY = (-(worldPieceA.PieceHeight / 2)+ worldPieceA.ExitList[exitBinaryA].coords.tileY /*+ .5f*/);
        float otherExitPosX = (-(worldPieceB.PieceWidth / 2) + worldPieceB.ExitList[exitBinaryB].coords.tileX /*+ .5f*/);
        float otherExitPosY = (-(worldPieceB.PieceHeight / 2) + worldPieceB.ExitList[exitBinaryB].coords.tileY /*+ .5f*/);
        float xOffset = exitPosX - otherExitPosX;
        float yOffset = exitPosY - otherExitPosY;
        Vector3 connectionOffset = new Vector3(xOffset, yOffset);
        worldPieceA.ExitConnectionOffsets[exitBinaryA] = connectionOffset;
        worldPieceB.ExitConnectionOffsets[exitBinaryB] = -connectionOffset;
    }



    int[] GetRandomCacheIDArray() {
        return Enumerable.Range(0, worldPieceCache.Count).OrderBy(r => rng.Next()).ToArray();
    }


    bool CheckWorldPieceExitCompatibility(Exit targetExit, WorldPiece worldPiece, out int compatibleExitBinary) {
        WallDir targetDir;
        switch (targetExit.wallDir) {
            case WallDir.NORTH:
                targetDir = WallDir.SOUTH;
                break;
            case WallDir.EAST:
                targetDir = WallDir.WEST;
                break;
            case WallDir.SOUTH:
                targetDir = WallDir.NORTH;
                break;
            case WallDir.WEST:
                targetDir = WallDir.EAST;
                break;
            default:
                Debug.Log("Failed to find target exit's wallDir");
                compatibleExitBinary = -1;
                return false;
        }
        foreach (Exit exit in worldPiece.ExitList.Values) {
            if (exit.wallDir == targetDir) {
                compatibleExitBinary = exit.binaryConfig;
                return true;
            }
        }
        compatibleExitBinary = -1;
        return false;
    }
}
