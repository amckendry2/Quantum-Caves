using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public int CurrentWidth { get; private set; } = 0;
    public int CurrentHeight { get; private set; } = 0;

    private static System.Random rng = new System.Random();

    [SerializeField] private int initialWidth = 50;
    [SerializeField] private int initialHeight = 25;

    [SerializeField] private bool randomizeMapSize = false;
    [SerializeField] private int minSize = 20;
    [SerializeField] private int maxSize = 100;

    [SerializeField] private string seed = "";
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int smoothAmount = 3;
    [SerializeField] private int wallThreshold = 4;
    [SerializeField] private int emptyThreshold = 4;
    [SerializeField] private int borderSize = 2;
    [SerializeField] private int minWallArea = 5;
    [SerializeField] private int minEmptyArea = 5;
    [SerializeField] private int maxRoomConnections = 1;
    [SerializeField] private int maxDistanceBetweenRooms = 15;
    [SerializeField] private int maxExitDistance = 30;
    [SerializeField] private int maxExitsPerRoom = 2;
    [SerializeField] private int spaceBetweenExits = 10;
    [SerializeField] private float minBorderExitDistance = 5;
    [SerializeField] private int passageWidth = 10;

    [Range(0, 100)]
    [SerializeField] private int randomFillPercent = 38;

    private int[,] map;
    private int[,] borderedMap;

    public List<Exit> Exits { get; private set; } = new List<Exit>();

    public int[,] GenerateMap() {
        int generationAttempts = 0;
        Exits.Clear();
        while (Exits.Count == 0) {
            Exits.Clear();
            generationAttempts++;
            if(generationAttempts > 10) {
                Debug.Log("couldn't generate map with exits");
                return null;
            }
            if (!randomizeMapSize) {
                CurrentWidth = initialWidth;
                CurrentHeight = initialHeight;
            } else {
                CurrentHeight = rng.Next(minSize, maxSize);
                CurrentWidth = rng.Next(minSize, maxSize);
            }
            if (CurrentHeight % 2 == 1) {
                CurrentHeight += 1;
            }
            if (CurrentWidth % 2 == 1) {
                CurrentWidth += 1;
            }
            GenerateBasicMap();
            for (int i = 0; i < smoothAmount; i++) {
                SmoothMap();
            }
            if (borderSize % 2 == 0) {
                borderSize += 1;
            }
            GenerateBorderedMap();
            ConsolidateRegions(out List<Room> remainingRooms);
            ConnectClosestRooms(remainingRooms);
            CreateExits(remainingRooms);
        }
        return borderedMap;
    }


    private void GenerateBasicMap() {
        map = new int[CurrentWidth, CurrentHeight];
        RandomFillMap();
    }


    private void RandomFillMap() {
        if (useRandomSeed) {
            seed = rng.Next().ToString();
        }
        System.Random pseudoRandom = new System.Random(seed.GetHashCode());
        for (int x = 0; x < CurrentWidth; x++) {
            for (int y = 0; y < CurrentHeight; y++) {
                map[x, y] = pseudoRandom.Next(0, 100) < randomFillPercent ? 1 : 0;
            }
        }
    }


    private void SmoothMap() {
        for (int x = 0; x < CurrentWidth; x++) {
            for (int y = 0; y < CurrentHeight; y++) {
                int neighborCount = GetNeighboringWallCount(x, y);
                if (neighborCount > wallThreshold)
                    map[x, y] = 1;
                else if (neighborCount < emptyThreshold)
                    map[x, y] = 0;
            }
        }
    }


    private int GetNeighboringWallCount(int gridX, int gridY) {
        int wallCount = 0;
        for (int x = gridX - 1; x <= gridX + 1; x++) {
            for (int y = gridY - 1; y <= gridY + 1; y++) {
                if (IsInMapRange(x, y)) {
                    wallCount += map[x, y];
                } else {
                    wallCount++;
                }
            }
        }
        return wallCount;
    }


    private bool IsInMapRange(int neighborX, int neighborY) {
        return (neighborX >= 0 && neighborX < CurrentWidth && neighborY >= 0 && neighborY < CurrentHeight);
    }


    private void GenerateBorderedMap() {
        borderedMap = new int[CurrentWidth + borderSize * 2, CurrentHeight + borderSize * 2];
        for (int x = 0; x < borderedMap.GetLength(0); x++) {
            for (int y = 0; y < borderedMap.GetLength(1); y++) {
                if (x >= borderSize && x < CurrentWidth + borderSize && y >= borderSize && y < CurrentHeight + borderSize) {
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                } else {
                    borderedMap[x, y] = 1;
                }
            }
        }
        CurrentWidth += borderSize * 2;
        CurrentHeight += borderSize * 2;
    }


    private void ConsolidateRegions(out List<Room> remainingRooms) {
        List<List<Coords>> wallRegions = GetRegions(1);
        foreach (List<Coords> region in wallRegions) {
            if (region.Count < minWallArea) {
                foreach (Coords tile in region) {
                    borderedMap[tile.tileX, tile.tileY] = 0;
                }
            }
        }
        remainingRooms = new List<Room>();
        List<List<Coords>> emptyRegions = GetRegions(0);
        foreach (List<Coords> region in emptyRegions) {
            if (region.Count < minEmptyArea) {
                foreach (Coords tile in region) {
                    borderedMap[tile.tileX, tile.tileY] = 1;
                }
            } else {
                remainingRooms.Add(new Room(region, borderedMap));
            }
        }
    }


    List<List<Coords>> GetRegions(int tileType) {
        List<List<Coords>> regions = new List<List<Coords>>();
        int[,] checkedTiles = new int[CurrentWidth, CurrentHeight];
        for (int x = 0; x < CurrentWidth; x++) {
            for (int y = 0; y < CurrentHeight; y++) {
                if (checkedTiles[x, y] == 0 && borderedMap[x, y] == tileType) {
                    List<Coords> tilesInRegion = GetTilesInRegion(x, y);
                    regions.Add(tilesInRegion);
                    foreach (Coords tile in tilesInRegion) {
                        checkedTiles[tile.tileX, tile.tileY] = 1;
                    }
                }
            }
        }
        return regions;
    }


    List<Coords> GetTilesInRegion(int startX, int startY) {
        List<Coords> tilesInRegion = new List<Coords>();
        int[,] checkedTiles = new int[CurrentWidth, CurrentHeight];
        int tileType = borderedMap[startX, startY];

        Queue<Coords> queue = new Queue<Coords>();
        queue.Enqueue(new Coords(startX, startY));
        checkedTiles[startX, startY] = 1;

        while (queue.Count > 0) {
            Coords tile = queue.Dequeue();
            tilesInRegion.Add(tile);
            for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++) {
                for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++) {
                    if (IsInMapRange(x, y) && (x == tile.tileX || y == tile.tileY)) {
                        if (checkedTiles[x, y] == 0 && borderedMap[x, y] == tileType) {
                            checkedTiles[x, y] = 1;
                            queue.Enqueue(new Coords(x, y));
                        }
                    }
                }
            }
        }
        return tilesInRegion;
    }


    void ConnectClosestRooms(List<Room> rooms) {
        int bestDistance = 0;
        Coords bestTileA = new Coords();
        Coords bestTileB = new Coords();
        Room bestRoomA = new Room();
        Room bestRoomB = new Room();
        bool connectionFound;
        foreach (Room roomA in rooms) {
            connectionFound = false;
            if (roomA.connectedRooms.Count >= maxRoomConnections)
                continue;
            foreach (Room roomB in rooms) {
                if (roomA.IsConnected(roomB) || roomA == roomB || roomB.connectedRooms.Count >= maxRoomConnections)
                    continue;
                for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++) {
                    for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++) {
                        Coords tileA = roomA.edgeTiles[tileIndexA];
                        Coords tileB = roomB.edgeTiles[tileIndexB];
                        int tileDistance = (int)(Mathf.Pow(tileA.tileX - tileB.tileX, 2) + Mathf.Pow(tileA.tileY - tileB.tileY, 2));
                        if (tileDistance < maxDistanceBetweenRooms && (!connectionFound || tileDistance < bestDistance)) {
                            bestDistance = tileDistance;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                            connectionFound = true;
                        }
                    }
                }

            }
            if (connectionFound) {
                CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            }
        }
    }



    void CreateExits(List<Room> rooms) {
        Exits.Clear();
        bool foundExit = false;
        foreach (Room room in rooms) {
            int bestDistance = 0;
            int bestTileIndex = 0;
            List<int> flaggedIndexes = new List<int>();
            for (int searchesIndex = 0; searchesIndex < maxExitsPerRoom; searchesIndex++) {
                Coords bestRoomTile = new Coords();
                Coords bestBorderTile = new Coords();
                for (int tileIndex = 0; tileIndex < room.edgeTiles.Count; tileIndex++) {
                    if (room.exits >= maxExitsPerRoom)
                        break;
                    if (IsTooCloseToExit(flaggedIndexes, tileIndex, room.edgeTiles.Count))
                        continue;
                    Coords tile = room.edgeTiles[tileIndex];
                    for (int x = 0; x < CurrentWidth; x++) {
                        for (int y = 0; y < CurrentHeight; y++) {
                            if (x == 0 || x == CurrentWidth - 1 || y == 0 || y == CurrentHeight - 1) {
                                if (!(x == CurrentWidth - 1 && y == CurrentHeight - 1) && !(x == CurrentWidth - 1 && y == 0) && !(y == CurrentHeight - 1 && x == 0)) {
                                    int distanceToBorder = (int)(Mathf.Pow(tile.tileX - x, 2f) + Mathf.Pow(tile.tileY - y, 2f));
                                    if (Exits.Count > 0) {
                                        bool tooClose = false;
                                        foreach (Exit exit in Exits) {
                                            if (Vector2.Distance(new Vector2(x, y), new Vector2(exit.coords.tileX, exit.coords.tileY)) < minBorderExitDistance) {
                                                tooClose = true;
                                                break;
                                            }
                                        }
                                        if (tooClose)
                                            continue;
                                    }
                                    if (distanceToBorder < maxExitDistance && (!foundExit || distanceToBorder < bestDistance)) {
                                        bestDistance = distanceToBorder;
                                        bestRoomTile = tile;
                                        bestBorderTile = new Coords(x, y);
                                        bestTileIndex = tileIndex;
                                        foundExit = true;
                                    }
                                }
                            }
                        }
                    }
                }
                if (foundExit) {
                    flaggedIndexes.Add(bestTileIndex);
                    room.AddExit();
                    int exitBinaryConfig = Exits.Count == 0 ? 1 : (int)Mathf.Pow(2f, Exits.Count);
                    Exits.Add(new Exit(bestBorderTile, GetExitWallDir(bestBorderTile), exitBinaryConfig));
                    ConnectExits(bestRoomTile, bestBorderTile);
                    foundExit = false;
                }
            }
        }
    }


    bool IsTooCloseToExit(List<int> flaggedIndexes, int index, int edgeTilesCount) {
        foreach (int flaggedIndex in flaggedIndexes) {
            if (Mathf.Abs(flaggedIndex - index) <= spaceBetweenExits || (edgeTilesCount - index) + flaggedIndex <= spaceBetweenExits)
                return true;
        }
        return false;
    }


    public WallDir GetExitWallDir(Coords tile) {
        float x = tile.tileX;
        float y = tile.tileY;
        if (x == 0)
            return WallDir.WEST;
        if (y == 0)
            return WallDir.SOUTH;
        if (y < CurrentHeight - 1)
            return WallDir.EAST;
        if (x < CurrentWidth - 1)
            return WallDir.NORTH;
        return WallDir.ERROR;
    }


    void ConnectExits(Coords roomTile, Coords borderTile) {
        List<Coords> line = GetLine(roomTile, borderTile);
        for (int i = 0; i < line.Count; i++) {
            DrawCircle(line[i], passageWidth);
        }
    }


    void CreatePassage(Room roomA, Room roomB, Coords tileA, Coords tileB) {
        Room.ConnectRooms(roomA, roomB);
        List<Coords> line = GetLine(tileA, tileB);
        for (int i = 0; i < line.Count; i++) {
            DrawCircle(line[i], passageWidth);
        }
    }


    List<Coords> GetLine(Coords from, Coords to) {
        List<Coords> line = new List<Coords>();
        int x = from.tileX;
        int y = from.tileY;
        int dx = to.tileX - x;
        int dy = to.tileY - y;
        int step = System.Math.Sign(dx);
        int gradientStep = System.Math.Sign(dy);
        int longest = System.Math.Abs(dx);
        int shortest = System.Math.Abs(dy);
        bool inverted = false;
        if (longest < shortest) {
            inverted = true;
            longest = System.Math.Abs(dy);
            shortest = System.Math.Abs(dx);
            step = System.Math.Sign(dy);
            gradientStep = System.Math.Sign(dx);
        }
        float gradient = longest / 2;
        for (int i = 0; i <= longest; i++) {
            line.Add(new Coords(x, y));
            if (inverted) {
                y += step;
            } else {
                x += step;
            }
            gradient += shortest;
            if (gradient >= longest) {
                if (inverted) {
                    x += gradientStep;
                } else {
                    y += gradientStep;
                }
                gradient -= longest;
            }
        }
        return line;
    }


    void DrawCircle(Coords c, int r) {
        for (int x = -r; x <= r; x++) {
            for (int y = -r; y <= r; y++) {
                if (x * x + y * y <= r * r) {
                    int realX = c.tileX + x;
                    int realY = c.tileY + y;
                    if (IsInMapRange(realX, realY))
                        borderedMap[realX, realY] = 0;

                }
            }
        }
    }


    Vector3 CoordsToWorldPoint(Coords coords) {
        return new Vector3(-(CurrentWidth) / 2 + 0.5f + coords.tileX, -(CurrentHeight) / 2 + 0.5f + coords.tileY, -.5f);
    }
}
