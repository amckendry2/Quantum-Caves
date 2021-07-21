using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldMeshGenerator : MonoBehaviour
{

    private List<Vector3> vertices;
    private List<int> triangles;
    private float wallHeight = 4;
    [SerializeField] private int mapSize = 500;
    private float squareSize = 1f;

    private int[,] worldMap;

    public int MapWidth { get; private set; }
    public int MapHeight { get; private set; }

    public void InitializeMap() {
        MapWidth = mapSize;
        MapHeight = mapSize;
        worldMap = new int[MapWidth, MapHeight];
        for (int x = 0; x < MapWidth; x++) {
            for (int y = 0; y < MapHeight; y++) {
                worldMap[x, y] = 1;
            }
        }
    }

    public Vector3 GetMapCenter() {
        return new Vector3(MapWidth / 2, MapHeight / 2);
    }

    public void ClearMap() {
        worldMap = new int[0, 0];
    }

    public void MapNewPiece(params int[] pieceCoords) {
        int bottomLeftX = pieceCoords[0];
        int bottomLeftY = pieceCoords[1];
        int topRightX = pieceCoords[2];
        int topRightY = pieceCoords[3];
        for (int x = bottomLeftX + 1; x < topRightX - 1; x++) {
            for (int y = bottomLeftY + 1; y < topRightY - 1; y++) {
                worldMap[x, y] = 0;
            }
        }
    }

    public void UnmapPiece(params int[] pieceCoords) {
        int bottomLeftX = pieceCoords[0];
        int bottomLeftY = pieceCoords[1];
        int topRightX = pieceCoords[2];
        int topRightY = pieceCoords[3];
        for (int x = bottomLeftX + 1; x < topRightX - 1; x++) {
            for (int y = bottomLeftY + 1; y < topRightY - 1; y++) {
                worldMap[x, y] = 1;
            }
        }
    }

    public void IncreaseMapSize(int borderSize) {
        int[,] newMap = new int[MapWidth + borderSize * 2, MapHeight + borderSize * 2];
        for (int x = 0; x < newMap.GetLength(0); x++) {
            for (int y = 0; y < newMap.GetLength(1); y++) {
                if (x > borderSize && x < MapWidth + borderSize && y > borderSize && y < MapHeight + borderSize) {
                    newMap[x, y] = worldMap[x, y];
                } else {
                    newMap[x, y] = 1;
                }
            }
        }
        worldMap = newMap;
    }

    private void Start() {

        // InitializeMap(100, 100);
        // MapPiece(41, 41, 60, 60);
        // GenerateWorldMesh(worldMap, 1f);

    }

    public void GenerateFloorMesh() {
        int firstIndex = vertices.Count;
        vertices.Add(new Vector3(-MapWidth / 2, -MapHeight / 2, wallHeight));
        vertices.Add(new Vector3(-MapWidth / 2, MapHeight / 2, wallHeight));
        vertices.Add(new Vector3(MapWidth / 2, MapHeight / 2, wallHeight));
        vertices.Add(new Vector3(MapWidth / 2, -MapHeight / 2, wallHeight));
        triangles.Add(firstIndex);
        triangles.Add(firstIndex + 1);
        triangles.Add(firstIndex + 2);
        triangles.Add(firstIndex);
        triangles.Add(firstIndex + 2);
        triangles.Add(firstIndex + 3);
    }


    public Mesh OptimizedGenerateWorldMesh(bool doFloor) {
        vertices = new List<Vector3>();
        triangles = new List<int>();
        WorldSquareGrid worldSquareGrid = new WorldSquareGrid(worldMap, squareSize);
        int xSquareCount = worldSquareGrid.worldSquares.GetLength(0);
        int ySquareCount = worldSquareGrid.worldSquares.GetLength(1);
        int[,] triangulatedSquares = new int[xSquareCount, ySquareCount];
        for (int x = 0; x < xSquareCount; x++) {
            for (int y = 0; y < ySquareCount; y++) {
                if (triangulatedSquares[x, y] == 0) {
                    int cornerDistance = 0;
                    for (int perimeterI = 1; perimeterI + x < xSquareCount && perimeterI + y < ySquareCount; perimeterI++) {
                        bool foundInactiveSquare = false;
                        for (int xI = x; xI <= x + perimeterI; xI++) {
                            if (!worldSquareGrid.worldSquares[xI, y + perimeterI].active)
                                foundInactiveSquare = true;
                        }
                        for (int yI = y; yI <= y + perimeterI; yI++) {
                            if (!worldSquareGrid.worldSquares[x + perimeterI, yI].active) {
                                foundInactiveSquare = true;
                            }
                        }
                        if (foundInactiveSquare) {
                            break;
                        } else {
                            cornerDistance = perimeterI;
                        }
                    }
                    if (cornerDistance > 0) {
                        for (int xJ = x; xJ <= x + cornerDistance; xJ++) {
                            for (int yJ = y; yJ <= y + cornerDistance; yJ++) {
                                triangulatedSquares[xJ, yJ] = 1;
                            }
                        }
                        TriangulateSection(x, y, cornerDistance, ref worldSquareGrid);
                    }
                }
            }
        }
        for (int x = 0; x < triangulatedSquares.GetLength(0); x++) {
            for (int y = 0; y < triangulatedSquares.GetLength(1); y++) {
                if (triangulatedSquares[x, y] == 0)
                    TriangulateSquare(worldSquareGrid.worldSquares[x, y]);
            }
        }
        if(doFloor)GenerateFloorMesh();
        Mesh worldMesh = new Mesh {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray()
        };
        worldMesh.RecalculateNormals();
        return worldMesh;
    }



    //public Mesh GenerateWorldMesh(float squareSize) {
    //    vertices = new List<Vector3>();
    //    triangles = new List<int>();
    //    WorldSquareGrid worldSquareGrid = new WorldSquareGrid(worldMap, squareSize);
    //    for (int x = 0; x < worldSquareGrid.worldSquares.GetLength(0); x++) {
    //        for (int y = 0; y < worldSquareGrid.worldSquares.GetLength(1); y++) {
    //            TriangulateSquare(worldSquareGrid.worldSquares[x, y]);
    //        }
    //    }
    //    GenerateFloorMesh();
    //    Mesh worldMesh = new Mesh();
    //    worldMesh.vertices = vertices.ToArray();
    //    worldMesh.triangles = triangles.ToArray();
    //    worldMesh.RecalculateNormals();
    //    return worldMesh;
    //}


    private void TriangulateSection(int startX, int startY, int size, ref WorldSquareGrid worldSquareGrid) {
        Node blNode = worldSquareGrid.worldSquares[startX, startY].nodeArray[3];
        Node tlNode = worldSquareGrid.worldSquares[startX, startY + size].nodeArray[0];
        Node trNode = worldSquareGrid.worldSquares[startX + size, startY + size].nodeArray[1];
        Node brNode = worldSquareGrid.worldSquares[startX + size, startY].nodeArray[2];
        Node[] nodeArray = new Node[4] { blNode, tlNode, trNode, brNode };
        foreach (Node node in nodeArray) {
            if (node.vertexIndex == -1) {
                node.vertexIndex = vertices.Count;
                vertices.Add(node.position);
            }
        }
        triangles.Add(blNode.vertexIndex);
        triangles.Add(tlNode.vertexIndex);
        triangles.Add(trNode.vertexIndex);

        triangles.Add(blNode.vertexIndex);
        triangles.Add(trNode.vertexIndex);
        triangles.Add(brNode.vertexIndex);
    }

    private void TriangulateSquare(WorldSquare square) {
        if (square.active) {
            foreach (Node node in square.nodeArray) {
                if (node.vertexIndex == -1) {
                    node.vertexIndex = vertices.Count;
                    vertices.Add(node.position);
                }
            }
            triangles.Add(square.nodeArray[0].vertexIndex);
            triangles.Add(square.nodeArray[1].vertexIndex);
            triangles.Add(square.nodeArray[2].vertexIndex);

            triangles.Add(square.nodeArray[0].vertexIndex);
            triangles.Add(square.nodeArray[2].vertexIndex);
            triangles.Add(square.nodeArray[3].vertexIndex);
        }
    }

    private class Node
    {
        public Vector3 position;
        public int vertexIndex = -1;
        public bool active;

        public Node(Vector3 _position, bool _active) {
            position = _position;
            active = _active;
        }
    }

    private class WorldSquare
    {
        public bool active = false;
        public Node[] nodeArray;
        public WorldSquare(Node _tl, Node _tr, Node _br, Node _bl) {
            nodeArray = new Node[] { _tl, _tr, _br, _bl };
            if (_tl.active && _tr.active && _br.active && _bl.active)
                active = true;
        }
    }

    private class WorldSquareGrid
    {
        public WorldSquare[,] worldSquares;
        public WorldSquareGrid(int[,] worldMap, float squareSize) {
            int nodeCountX = worldMap.GetLength(0);
            int nodeCountY = worldMap.GetLength(1);
            float width = (nodeCountX - 1) * squareSize;
            float height = (nodeCountY - 1) * squareSize;
            Node[,] nodes = new Node[nodeCountX, nodeCountY];
            for (int x = 0; x < nodeCountX; x++) {
                for (int y = 0; y < nodeCountY; y++) {
                    Vector3 position = new Vector3(-width / 2 + x * squareSize, -height / 2 + y * squareSize, 0);
                    nodes[x, y] = new Node(position, worldMap[x, y] == 1);
                }
            }
            worldSquares = new WorldSquare[nodeCountX - 1, nodeCountY - 1];
            for (int x = 0; x < nodeCountX - 1; x++) {
                for (int y = 0; y < nodeCountY - 1; y++) {
                    worldSquares[x, y] = new WorldSquare(
                            nodes[x, y + 1],
                            nodes[x + 1, y + 1],
                            nodes[x + 1, y],
                            nodes[x, y]);
                }
            }
        }
    }




}
