using System.Collections;
using System.Collections.Generic;
using UnityEngine;


class Room
{
    public List<Coords> tiles;
    public List<Coords> edgeTiles;
    public List<Room> connectedRooms;
    public int roomSize;
    public int exits = 0;
    public Room() {

    }
    public Room(List<Coords> _tiles, int[,] borderedMap) {
        tiles = _tiles;
        roomSize = tiles.Count;
        edgeTiles = new List<Coords>();
        connectedRooms = new List<Room>();
        foreach (Coords tile in tiles) {
            for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++) {
                for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++) {
                    if (x >= 0 && x < borderedMap.GetLength(0) && y >= 0 && y < borderedMap.GetLength(1)) {
                        if (borderedMap[x, y] == 1 && (x == tile.tileX || y == tile.tileY))
                            edgeTiles.Add(tile);
                    }
                }
            }
        }
    }
    public static void ConnectRooms(Room roomA, Room roomB) {
        roomA.connectedRooms.Add(roomB);
        roomB.connectedRooms.Add(roomA);
    }
    public bool IsConnected(Room otherRoom) {
        return connectedRooms.Contains(otherRoom);
    }
    public void AddExit() => exits++;
}


public class Exit
{
    public Coords coords;
    public WallDir wallDir;
    public int binaryConfig;
    public bool active = false;
    public List<Vector3> perimeter;
    public Exit() {
    }
    public Exit(Coords _coords, WallDir _wallDir, int _binaryConfig) {
        coords = _coords;
        wallDir = _wallDir;
        binaryConfig = _binaryConfig;
    }
    public void RotateClockwise(float width) {
        switch (wallDir) {
            case WallDir.NORTH:
                wallDir = WallDir.EAST;
                break;
            case WallDir.EAST:
                wallDir = WallDir.SOUTH;
                break;
            case WallDir.SOUTH:
                wallDir = WallDir.WEST;
                break;
            case WallDir.WEST:
                wallDir = WallDir.NORTH;
                break;
        }
        float gridWidth = width - 1;
        float x = coords.tileY;
        float y = gridWidth - coords.tileX;
        coords.tileX = (int)x;
        coords.tileY = (int)y;
    }
}

public enum WallDir
{
    NORTH = 0,
    EAST = 3,
    SOUTH = 2,
    WEST = 1,
    ERROR
}

public struct Coords
{
    public int tileX;
    public int tileY;
    public Coords(int x, int y) {
        tileX = x;
        tileY = y;
    }
}


public struct CombinedMeshesInstance
{
    public Mesh ceiling;
    public Mesh walls;
    public int binaryConfig;
    public CombinedMeshesInstance(Mesh _c, Mesh _w, int _bc) {
        ceiling = _c;
        walls = _w;
        binaryConfig = _bc;
    }
}

public static class LinqHelper
{
    public static IEnumerable<T[]> Combinations<T>(this T[] values, int k) {
        if (k < 0 || values.Length < k)
            yield break; // invalid parameters, no combinations possible

        // generate the initial combination indices
        var combIndices = new int[k];
        for (var i = 0; i < k; i++) {
            combIndices[i] = i;
        }

        while (true) {
            // return next combination
            var combination = new T[k];
            for (var i = 0; i < k; i++) {
                combination[i] = values[combIndices[i]];
            }
            yield return combination;

            // find first index to update
            var indexToUpdate = k - 1;

            while (indexToUpdate >= 0 && combIndices[indexToUpdate] >= values.Length - k + indexToUpdate) {
                indexToUpdate--;
            }

            if (indexToUpdate < 0)
                yield break; // done

            // update combination indices
            for (var combIndex = combIndices[indexToUpdate] + 1; indexToUpdate < k; indexToUpdate++, combIndex++) {
                combIndices[indexToUpdate] = combIndex;
            }
        }
    }
}