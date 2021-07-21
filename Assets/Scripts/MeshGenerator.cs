using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour
{

    List<Vector3> vertices;
    List<int> triangles;
    List<List<int>> borders;
    HashSet<int> checkedVertices;

    Dictionary<int, List<Triangle>> triangleDictionary;

    private SquareGrid squareGrid;

    [SerializeField] private float wallHeight = 5;
    [SerializeField] private float squareSize = 1;


    public Mesh GenerateCeilingMesh(int[,] map) {
        borders = new List<List<int>>();
        checkedVertices = new HashSet<int>();
        vertices = new List<Vector3>();
        triangles = new List<int>();
        triangleDictionary = new Dictionary<int, List<Triangle>>();

        squareGrid = new SquareGrid(map, squareSize);

        for (int x = 0; x < squareGrid.squares.GetLength(0); x++) {
            for (int y = 0; y < squareGrid.squares.GetLength(1); y++) {
                TriangulateSquare(squareGrid.squares[x, y]);
            }
        }

        Mesh ceilingMesh = new Mesh {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray()
        };

        ceilingMesh.RecalculateNormals();

        return ceilingMesh;

    }

    public Mesh GenerateWallsMesh() {

        CalculateMeshOutlines();

        List<Vector3> wallVertices = new List<Vector3>();
        List<int> wallTriangles = new List<int>();
        Mesh wallMesh = new Mesh();
        foreach (List<int> outline in borders) {
            for (int i = 0; i < outline.Count; i++) {

                int startIndex = wallVertices.Count;

                wallVertices.Add(vertices[outline[i]]);
                wallVertices.Add(vertices[outline[i]] + Vector3.forward * wallHeight);

                if (i > 0) {

                    wallTriangles.Add(startIndex);
                    wallTriangles.Add(startIndex - 1);
                    wallTriangles.Add(startIndex - 2);

                    wallTriangles.Add(startIndex);
                    wallTriangles.Add(startIndex + 1);
                    wallTriangles.Add(startIndex - 1);

                }
            }

        }
        wallMesh.vertices = wallVertices.ToArray();
        wallMesh.triangles = wallTriangles.ToArray();
        wallMesh.RecalculateNormals();

        return wallMesh;
    }

    struct Triangle
    {
        public int vertexIndexA;
        public int vertexIndexB;
        public int vertexIndexC;
        int[] vertices;

        public Triangle(int a, int b, int c) {
            vertexIndexA = a;
            vertexIndexB = b;
            vertexIndexC = c;
            vertices = new int[3] { a, b, c };
        }
        public int this[int i] {
            get {
                return vertices[i];
            }
        }
    }

    void TriangulateSquare(Square square) {
        switch (square.configuration) {
            case 0:
                break;
            //1 point
            case 1:
                MeshFromPoints(square.centerLeft, square.centerBottom, square.bottomLeft);
                break;
            case 2:
                MeshFromPoints(square.bottomRight, square.centerBottom, square.centerRight);
                break;
            case 4:
                MeshFromPoints(square.topRight, square.centerRight, square.centerTop);
                break;
            case 8:
                MeshFromPoints(square.topLeft, square.centerTop, square.centerLeft);
                break;
            //2 points rect
            case 3:
                MeshFromPoints(square.centerLeft, square.centerRight, square.bottomRight, square.bottomLeft);
                break;
            case 6:
                MeshFromPoints(square.topRight, square.bottomRight, square.centerBottom, square.centerTop);
                break;
            case 9:
                MeshFromPoints(square.topLeft, square.centerTop, square.centerBottom, square.bottomLeft);
                break;
            case 12:
                MeshFromPoints(square.topLeft, square.topRight, square.centerRight, square.centerLeft);
                break;
            //2 points diagonal
            case 5:
                MeshFromPoints(square.centerTop, square.topRight, square.centerRight, square.centerBottom, square.bottomLeft, square.centerLeft);
                break;
            case 10:
                MeshFromPoints(square.topLeft, square.centerTop, square.centerRight, square.bottomRight, square.centerBottom, square.centerLeft);
                break;
            //3 points
            case 7:
                MeshFromPoints(square.topRight, square.bottomRight, square.bottomLeft, square.centerLeft, square.centerTop);
                break;
            case 11:
                MeshFromPoints(square.bottomRight, square.bottomLeft, square.topLeft, square.centerTop, square.centerRight);
                break;
            case 13:
                MeshFromPoints(square.bottomLeft, square.topLeft, square.topRight, square.centerRight, square.centerBottom);
                break;
            case 14:
                MeshFromPoints(square.topLeft, square.topRight, square.bottomRight, square.centerBottom, square.centerLeft);
                break;
            //4 points
            case 15:
                MeshFromPoints(square.topLeft, square.topRight, square.bottomRight, square.bottomLeft);
                break;
        }
    }

    void MeshFromPoints(params Node[] points) {
        AssignVertices(points);
        if (points.Length >= 3)
            CreateTriangle(points[0], points[1], points[2]);
        if (points.Length >= 4)
            CreateTriangle(points[0], points[2], points[3]);
        if (points.Length >= 5)
            CreateTriangle(points[0], points[3], points[4]);
        if (points.Length == 6)
            CreateTriangle(points[0], points[4], points[5]);
    }

    void AssignVertices(params Node[] points) {
        for (int i = 0; i < points.Length; i++) {
            if (points[i].vertexIndex == -1) {
                points[i].vertexIndex = vertices.Count;
                vertices.Add(points[i].position);
            }
        }
    }

    void CreateTriangle(Node a, Node b, Node c) {
        Triangle triangle = new Triangle(a.vertexIndex, b.vertexIndex, c.vertexIndex);
        for (int i = 0; i < 3; i++) {
            int vertexIndex = triangle[i];
            triangles.Add(vertexIndex);
            if (!triangleDictionary.ContainsKey(vertexIndex))
                triangleDictionary.Add(vertexIndex, new List<Triangle>());
            triangleDictionary[vertexIndex].Add(triangle);
        }
    }

    void CalculateMeshOutlines() {
        for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++) {
            if (!checkedVertices.Contains(vertexIndex)) {
                if (GetConnectedBorderVertex(vertexIndex) != -1) {
                    List<int> outlineVertices = new List<int>();
                    FollowOutline(vertexIndex, ref outlineVertices);
                    if (IsOutlineEdge(vertexIndex, outlineVertices[outlineVertices.Count - 1]))
                        outlineVertices.Add(vertexIndex);
                    borders.Add(outlineVertices);
                } else {
                    checkedVertices.Add(vertexIndex);
                }
            }
        }
    }

    void FollowOutline(int vertex, ref List<int> outlineVertices) {
        outlineVertices.Add(vertex);
        checkedVertices.Add(vertex);

        int nextVertex = GetConnectedBorderVertex(vertex);
        if (nextVertex != -1 && !checkedVertices.Contains(nextVertex)) {
            FollowOutline(nextVertex, ref outlineVertices);
        }
    }

    int GetConnectedBorderVertex(int vertexIndex) {
        if (triangleDictionary.ContainsKey(vertexIndex)) {
            List<Triangle> connectedTriangles = triangleDictionary[vertexIndex];
            foreach (Triangle triangle in connectedTriangles) {
                for (int i = 0; i < 3; i++) {
                    if (triangle[i] != vertexIndex) {
                        if (IsOutlineEdge(vertexIndex, triangle[i]) && !checkedVertices.Contains(triangle[i])) {
                            if (!CheckVertexOrder(vertexIndex, triangle[i], triangle))
                                continue;
                            return triangle[i];
                        }
                    }
                }
            }
        }
        return -1;
    }

    bool IsOutlineEdge(int vertexA, int vertexB) {
        List<Triangle> triangleListA = triangleDictionary[vertexA];
        List<Triangle> triangleListB = triangleDictionary[vertexB];
        return (triangleListA.Except(triangleListB).Count() == triangleListA.Count - 1);
    }

    bool CheckVertexOrder(int startingVertex, int endingVertex, Triangle triangle) {
        int startingIndex = 0;
        int endingIndex = 0;
        for (int i = 0; i < 3; i++) {
            if (triangle[i] == startingVertex)
                startingIndex = i;
            if (triangle[i] == endingVertex)
                endingIndex = i;
        }
        return (startingIndex > endingIndex || (startingIndex == 0 && endingIndex == 2));
    }


    public class SquareGrid
    {
        public Square[,] squares;
        public SquareGrid(int[,] map, float squareSize) {
            int nodeCountX = map.GetLength(0);
            int nodeCountY = map.GetLength(1);
            float width = nodeCountX * squareSize;
            float height = nodeCountY * squareSize;
            ControlNode[,] controlNodes = new ControlNode[nodeCountX, nodeCountY];

            for (int x = 0; x < nodeCountX; x++) {
                for (int y = 0; y < nodeCountY; y++) {
                    Vector3 pos = new Vector3(-width / 2 + x * squareSize + squareSize / 2, -height / 2 + y * squareSize + squareSize / 2, 0);
                    controlNodes[x, y] = new ControlNode(pos, map[x, y] == 1, squareSize);
                }
            }

            squares = new Square[nodeCountX - 1, nodeCountY - 1];

            for (int x = 0; x < nodeCountX - 1; x++) {
                for (int y = 0; y < nodeCountY - 1; y++) {
                    squares[x, y] = new Square(
                        controlNodes[x, y + 1],
                        controlNodes[x + 1, y + 1],
                        controlNodes[x, y],
                        controlNodes[x + 1, y]);
                }
            }
        }
    }

    public class Square
    {
        public ControlNode topLeft, topRight, bottomLeft, bottomRight;
        public Node centerLeft, centerTop, centerRight, centerBottom;
        public int configuration;
        public Square(ControlNode _topLeft, ControlNode _topRight, ControlNode _bottomLeft, ControlNode _bottomRight) {

            topLeft = _topLeft;
            topRight = _topRight;
            bottomLeft = _bottomLeft;
            bottomRight = _bottomRight;

            centerLeft = bottomLeft.above;
            centerTop = topLeft.right;
            centerRight = bottomRight.above;
            centerBottom = bottomLeft.right;

            if (topLeft.active)
                configuration += 8;
            if (topRight.active)
                configuration += 4;
            if (bottomRight.active)
                configuration += 2;
            if (bottomLeft.active)
                configuration += 1;
        }
    }

    public class Node
    {
        public Vector3 position;
        public int vertexIndex = -1;
        public Node(Vector3 _pos) {
            position = _pos;
        }
    }

    public class ControlNode : Node
    {
        public bool active;
        public Node above, right;
        public ControlNode(Vector3 _pos, bool _active, float squareSize) : base(_pos) {
            active = _active;
            above = new Node(position + Vector3.up * squareSize / 2f);
            right = new Node(position + Vector3.right * squareSize / 2f);
        }
    }
}
