using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class WorldPiece {

    public readonly int ID;

    public bool usedInMap = false;
    private bool doFloor;

    public readonly Mesh CeilingMesh;
    public readonly Mesh WallsMesh;

    public int PieceWidth { get; private set; }
    public int PieceHeight { get; private set; }

    public Dictionary<int, int> NavigationNetwork;
    public Dictionary<int, Exit> ExitList;
    public Dictionary<int, Vector3> ExitConnectionOffsets;
    public Dictionary<int, CombinedMeshesInstance> QuantumMeshConfigs { get; private set; }
    public Dictionary<WallDir, List<int>> ExitConflicts = new Dictionary<WallDir, List<int>>();
    public List<Vector3> BeaconList = new List<Vector3>();

    private WorldMeshGenerator worldMeshGenerator;

    public WorldPiece(MapGenerator mapGen, MeshGenerator meshGen, WorldMeshGenerator worldGen, int _ID, bool _doFloor) {
        doFloor = _doFloor;
        worldMeshGenerator = worldGen;
        ExitList = new Dictionary<int, Exit>();
        NavigationNetwork = new Dictionary<int, int>();
        ExitConnectionOffsets = new Dictionary<int, Vector3>();
        CeilingMesh = meshGen.GenerateCeilingMesh(mapGen.GenerateMap());
        WallsMesh = meshGen.GenerateWallsMesh();
        PieceWidth = mapGen.CurrentWidth;
        PieceHeight = mapGen.CurrentHeight;
        Debug.Log("new world piece has " + mapGen.Exits.Count + " exits");
        foreach(Exit exit in mapGen.Exits) {
            if (ExitConflicts.ContainsKey(exit.wallDir)) {
                ExitConflicts[exit.wallDir].Add(exit.binaryConfig);
            } else {
                ExitConflicts.Add(exit.wallDir, new List<int> { exit.binaryConfig });
            }
            ExitList.Add(exit.binaryConfig, exit);
            NavigationNetwork.Add(exit.binaryConfig, -1);
            ExitConnectionOffsets.Add(exit.binaryConfig, Vector3.zero);
    //        exit.AssignToWorldPiece(this);
        }
        ID = _ID;
    }

    public WorldPiece(WorldPieceConfig config) {
        doFloor = config.doFloor;
        usedInMap = true;
        worldMeshGenerator = config.worldMeshGenerator;
        CeilingMesh = config.CeilingMesh;
        WallsMesh = config.WallsMesh;
        PieceWidth = config.PieceWidth;
        PieceHeight = config.PieceHeight;
        ID = config.ID;
        ExitConflicts = config.ExitConflicts;
        ExitList = config.ExitList;
        NavigationNetwork = config.NavigationNetwork;
        ExitConnectionOffsets = config.ExitConnectionOffsets;
    }

    public WorldPiece GetClone(int _ID) {
        WorldPieceConfig config = new WorldPieceConfig();
        config.doFloor = doFloor;
        config.worldMeshGenerator = worldMeshGenerator;
        config.ID = _ID;
        config.CeilingMesh = CeilingMesh;
        config.WallsMesh = WallsMesh;
        config.PieceWidth = PieceWidth;
        config.PieceHeight = PieceHeight;
        config.ExitConflicts = ExitConflicts;
        config.ExitList = ExitList;
        config.NavigationNetwork = NavigationNetwork.ToDictionary(entry => entry.Key, entry => entry.Value);
        config.ExitConnectionOffsets = ExitConnectionOffsets.ToDictionary(entry => entry.Key, entry => entry.Value);
        return new WorldPiece(config);
    }

    private Dictionary<int, int> GetNetworkNavigationClone() {
        Dictionary<int, int> clone = new Dictionary<int, int>();
        foreach (KeyValuePair<int,int> entry in NavigationNetwork) {
            clone.Add(entry.Key, entry.Value);
        }
        return clone;
    }


    public void GenerateQuantumMesh(Dictionary<int, WorldPiece> worldPieceCache) {
        QuantumMeshConfigs = new Dictionary<int, CombinedMeshesInstance>();
        worldMeshGenerator.InitializeMap();
        int[] centerPieceWorldCoords = GetPieceWorldCoords(Vector3.zero, PieceHeight, PieceWidth);
        worldMeshGenerator.MapNewPiece(centerPieceWorldCoords);
        CombineInstance zeroWallsCombineInstance = new CombineInstance {
            mesh = WallsMesh,
            transform = Matrix4x4.identity
        };
        CombineInstance zeroCeilingCombineInstance = new CombineInstance {
            mesh = CeilingMesh,
            transform = Matrix4x4.identity
        };
        CombineInstance worldCombineInstance = new CombineInstance {
            mesh = worldMeshGenerator.OptimizedGenerateWorldMesh(doFloor),
            transform = Matrix4x4.identity
        };
        CombineInstance[] zeroCeilingCombine = new CombineInstance[] {
            zeroCeilingCombineInstance, worldCombineInstance};
        Mesh zeroCombinedCeilingMeshes = new Mesh();
        zeroCombinedCeilingMeshes.CombineMeshes(zeroCeilingCombine);
        zeroCombinedCeilingMeshes = CombineVertices(zeroCombinedCeilingMeshes);
        CombinedMeshesInstance zeroConfig = new CombinedMeshesInstance {
            ceiling = zeroCombinedCeilingMeshes,
            walls = WallsMesh
        };
        QuantumMeshConfigs.Add(0, zeroConfig);
        int[] exitArray = NavigationNetwork.Keys.ToArray();
        for(int i = 1; i <= exitArray.Length; i++) {
            int numCombinations = LinqHelper.Combinations(exitArray, i).Count();
            foreach(int[] combination in LinqHelper.Combinations(exitArray, i)) {
                int config = 0;
                worldMeshGenerator.InitializeMap();
                worldMeshGenerator.MapNewPiece(centerPieceWorldCoords);
                List<CombineInstance> wallsCombine = new List<CombineInstance>();
                List<CombineInstance> ceilingCombine = new List<CombineInstance>();
                wallsCombine.Add(zeroWallsCombineInstance);
                ceilingCombine.Add(zeroCeilingCombineInstance);
                foreach (int exitBinary in combination) {
                    config += exitBinary;
                    WorldPiece piece = worldPieceCache[NavigationNetwork[exitBinary]];
                    Vector3 connectionOffset = ExitConnectionOffsets[exitBinary];
                    Matrix4x4 meshOffset = Matrix4x4.TRS(connectionOffset, Quaternion.identity, Vector3.one);
                    CombineInstance pieceWallsCombineInstance = new CombineInstance {
                        mesh = piece.WallsMesh,
                        transform = meshOffset
                    };
                    CombineInstance pieceCeilingCombineInstance = new CombineInstance {
                        mesh = piece.CeilingMesh,
                        transform = meshOffset
                    };
                    wallsCombine.Add(pieceWallsCombineInstance);
                    ceilingCombine.Add(pieceCeilingCombineInstance);
                    int[] worldCoords = GetPieceWorldCoords(connectionOffset, piece.PieceHeight, piece.PieceWidth);
                    worldMeshGenerator.MapNewPiece(worldCoords);
                }
                worldCombineInstance.mesh = worldMeshGenerator.OptimizedGenerateWorldMesh(doFloor);
                ceilingCombine.Add(worldCombineInstance);
                Mesh combinedCeilingMesh = new Mesh();
                Mesh combinedWallsMesh = new Mesh();
                combinedCeilingMesh.CombineMeshes(ceilingCombine.ToArray());
                combinedWallsMesh.CombineMeshes(wallsCombine.ToArray());
                combinedCeilingMesh = CombineVertices(combinedCeilingMesh);
                combinedWallsMesh = CombineVertices(combinedWallsMesh);
                CombinedMeshesInstance quantumMesh = new CombinedMeshesInstance {
                    ceiling = combinedCeilingMesh,
                    walls = combinedWallsMesh
                };
                QuantumMeshConfigs.Add(config, quantumMesh);
            }
        }
    }

    public int[] GetPieceWorldCoords(Vector3 connectionOffset, float pieceHeight, float pieceWidth) {
        Vector3 worldMapCenter = worldMeshGenerator.GetMapCenter();
        float centerX = worldMapCenter.x + connectionOffset.x;
        float centerY = worldMapCenter.y + connectionOffset.y;
        float bottomLeftX = centerX - pieceWidth / 2;
        float bottomLeftY = centerY - pieceHeight / 2;
        float topRightX = centerX + pieceWidth / 2;
        float topRightY = centerY + pieceHeight / 2;
        if (bottomLeftX % 1 != 0 || bottomLeftY % 1 != 0 || topRightX % 1 != 0 || topRightY % 1 != 0)
            Debug.Log("Error: bad value in grid coordinates");
        return new int[4] { (int)bottomLeftX, (int)bottomLeftY, (int)topRightX, (int)topRightY };
    }

    public Dictionary<int, List<Vector3>> GetExitPerimeterPositions(Vector3 currentWorldPos) {
        Dictionary<int, List<Vector3>> exitPerimeterPositions = new Dictionary<int, List<Vector3>>();
        foreach (Exit exit in ExitList.Values) {
            List<Vector3> perimeter = ExpandPointToSquare(GetWorldPosOfCoords(currentWorldPos, exit.coords), 1.5f);
            exitPerimeterPositions.Add(exit.binaryConfig, perimeter);
        }
        return exitPerimeterPositions;
    }

    public Vector3 GetWorldPosOfCoords(Vector3 currentWorldPos, Coords coords) {
        float posX = (currentWorldPos.x - PieceWidth / 2) + coords.tileX;
        float posY = (currentWorldPos.y - PieceHeight / 2) + coords.tileY;
        return new Vector3(posX, posY, 1.5f);
    }


    private List<Vector3> ExpandPointToSquare(Vector3 point, float squareSize) {
        List<Vector3> perimeterPoints = new List<Vector3>();
        for (float i = -(squareSize/2) ; i <= 0; i+=.1f) {
            Vector3 leftSide = new Vector3(point.x - (squareSize / 2), point.y + i);
            Vector3 topSide = new Vector3(point.x + i, point.y + (squareSize / 2));
            Vector3 rightSide = new Vector3(point.x + (squareSize / 2), point.y - i);
            Vector3 bottomSide = new Vector3(point.x - i, point.y - (squareSize / 2));
            perimeterPoints.Add(leftSide);
            perimeterPoints.Add(topSide);
            perimeterPoints.Add(rightSide);
            perimeterPoints.Add(bottomSide);
        }
        return perimeterPoints;
    }

    Mesh CombineVertices(Mesh mesh) {
        Dictionary<Vector3, int> newGraph = new Dictionary<Vector3, int>();
        Vector3[] meshVertices = mesh.vertices;
        List<Vector3> newVertices = new List<Vector3>();
        int[] meshTriangles = mesh.triangles;
        int[] newTriangles = new int[meshTriangles.Length];
        for (int i = 0; i < meshTriangles.Length; i++) {
            int vertexIndex = meshTriangles[i];
            Vector3 vertex = meshVertices[vertexIndex];
            Vector3 correctedVertex = new Vector3();
            for (int j = 0; j < 3; j++) {
                correctedVertex[j] = Mathf.Round(vertex[j] / .5f) * .5f;
            }
            if (newGraph.ContainsKey(correctedVertex)) {

                newTriangles[i] = newGraph[correctedVertex];
            } else {
                int newIndex = newVertices.Count;
                newVertices.Add(correctedVertex);
                newGraph.Add(correctedVertex, newIndex);
                newTriangles[i] = newIndex;
            }
        }
        Mesh newMesh = new Mesh {
            vertices = newVertices.ToArray(),
            triangles = newTriangles
        };
        newMesh.RecalculateNormals();
        return newMesh;
    }

}

public struct WorldPieceConfig
{
    public bool doFloor;
    public WorldMeshGenerator worldMeshGenerator;
    public int ID;
    public Mesh CeilingMesh;
    public Mesh WallsMesh;
    public CombineInstance CeilingMeshCombineInstance;
    public CombineInstance WallsMeshCombineInstance;
    public int PieceWidth;
    public int PieceHeight;
    public Dictionary<WallDir, List<int>> ExitConflicts;
    public Dictionary<int, Exit> ExitList;
    public Dictionary<int, int> NavigationNetwork;
    public Dictionary<int, Vector3> ExitConnectionOffsets;
}
