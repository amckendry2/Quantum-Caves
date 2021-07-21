using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{

    [SerializeField] float movementSpeed = 1f;
    [SerializeField] float turnSpeed = 1f;
    private InputState inputState;
    [SerializeField] float collisionRaycastLength = 0.1f;
    CollisionHolder collisionHolder;
    public Camera mainCamera;
    private CharacterController characterController;
    

    void Start() {
        characterController = GetComponent<CharacterController>();
    }

    void Update() {
        GetInput();
    }

    private void FixedUpdate() {
        //     GetCollisions();
        //     MovePlayer();
        MoveWithController();
        mainCamera.transform.position = new Vector3(transform.position.x, transform.position.y, mainCamera.transform.position.z);
    }

    void MoveWithController() {
        Vector3 moveDir = new Vector3(inputState.horizontal, inputState.vertical, 0f);
        moveDir *= movementSpeed;
        characterController.Move(moveDir * Time.deltaTime);
    }

    void GetInput() {
        int horizontal = 0;
        int vertical = 0;
        int turn = 0;
        if (Input.GetKey(KeyCode.A))
            turn -= 1;
        if (Input.GetKey(KeyCode.D))
            turn += 1;
        if (Input.GetKey(KeyCode.RightArrow))
            horizontal += 1;
        if (Input.GetKey(KeyCode.LeftArrow))
            horizontal -= 1;
        if (Input.GetKey(KeyCode.UpArrow))
            vertical += 1;
        if (Input.GetKey(KeyCode.DownArrow))
            vertical -= 1;
        inputState = new InputState(horizontal, vertical, turn);
    }

    struct InputState
    {
        public int horizontal;
        public int vertical;
        public int turn;
        public bool neutral;
        public InputState(int _h, int _v, int _t) {
            horizontal = _h;
            vertical = _v;
            turn = _t;
            neutral = (_h == 0 && _v == 0 && _t == 0);
        }
    }

    public List<int> GetVisibleExitsList(Dictionary<int, List<Vector3>> exitPerimeters) {
        Vector3 pPos = transform.position;
        List<int> visibleExits = new List<int>();
        foreach (KeyValuePair<int, List<Vector3>> entry in exitPerimeters) {
            foreach (Vector3 pos in entry.Value) {
                Vector3 direction = Vector3.Normalize(new Vector3(pos.x - pPos.x, pos.y - pPos.y, 0f));
                float distance = Mathf.Sqrt(Mathf.Pow((pos.x - pPos.x), 2) + Mathf.Pow((pos.y - pPos.y), 2));
                if (!Physics.Raycast(transform.position, direction, distance, LayerMask.GetMask("Navigation"))) {
                    Debug.DrawLine(transform.position, pos, Color.red, 1f);
                    visibleExits.Add(entry.Key);
                    break;
                }
            }
        }
        return visibleExits;  
    }

    public bool CheckForPlayerExit(Vector3 currentWorldPosition, WorldPiece currentPiece, out int nextActivePieceBinary) {
        Vector3 playerPos = transform.position;
        float xMin = currentWorldPosition.x - (currentPiece.PieceWidth / 2);
        float xMax = currentWorldPosition.x + (currentPiece.PieceWidth / 2);
        float yMin = currentWorldPosition.y - (currentPiece.PieceHeight / 2);
        float yMax = currentWorldPosition.y + (currentPiece.PieceHeight / 2);
        bool foundExit = false;
        nextActivePieceBinary = -1;
        if (playerPos.x < xMin || playerPos.x > xMax || playerPos.y < yMin || playerPos.y > yMax) {
            float closestDistance = 0;
            Exit closestExit = new Exit();
            foreach (Exit exit in currentPiece.ExitList.Values) {
                float ePosX = (currentWorldPosition.x - (currentPiece.PieceWidth / 2)) + exit.coords.tileX;
                float ePosY = (currentWorldPosition.y - (currentPiece.PieceHeight/ 2)) + exit.coords.tileY;
                float distance = Vector3.Distance(playerPos, new Vector3(ePosX, ePosY));
                if (!foundExit || distance < closestDistance) {
                    closestDistance = distance;
                    closestExit = exit;
                    foundExit = true;
                }
            }
            nextActivePieceBinary = closestExit.binaryConfig;
        }
        return foundExit;
    }



    void GetCollisions() {
        bool north = false;
        bool east = false;
        bool south = false;
        bool west = false;
        for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
                if (x == 0 && y == 0)
                    continue;
                Vector3 rayDirection = new Vector3(x, y, 0f);
                float raycastLength = collisionRaycastLength;
                if (Physics.Raycast(transform.position, rayDirection, raycastLength, LayerMask.GetMask("Navigation"))) {
                    if (x > 0)
                        east = true;
                    if (x < 0)
                        west = true;
                    if (y > 0)
                        north = true;
                    if (y < 0)
                        south = true;
                }
            }
        }
        collisionHolder = new CollisionHolder(north, east, south, west);
    }

    struct CollisionHolder
    {
        public bool north;
        public bool east;
        public bool south;
        public bool west;
        public CollisionHolder(bool n, bool e, bool s, bool w) {
            north = n;
            east = e;
            south = s;
            west = w;
        }
    }

    void MovePlayer() {
        if (inputState.neutral)
            return;

        if (inputState.horizontal != 0 || inputState.vertical != 0) {

            float xMin = collisionHolder.west ? 0 : float.MinValue;
            float xMax = collisionHolder.east ? 0 : float.MaxValue;
            float yMin = collisionHolder.south ? 0 : float.MinValue;
            float yMax = collisionHolder.north ? 0 : float.MaxValue;

            float horizontalAcceleration = inputState.horizontal * movementSpeed * Time.fixedDeltaTime;
            float verticalAcceleration = inputState.vertical * movementSpeed * Time.fixedDeltaTime;

            horizontalAcceleration = Mathf.Clamp(horizontalAcceleration, xMin, xMax);
            verticalAcceleration = Mathf.Clamp(verticalAcceleration, yMin, yMax);

            Vector3 currentPosition = transform.position;
            Vector3 nextPosition = new Vector3(currentPosition.x + horizontalAcceleration, currentPosition.y + verticalAcceleration, currentPosition.z);

            transform.position = nextPosition;
            mainCamera.transform.position = new Vector3(nextPosition.x, nextPosition.y, mainCamera.transform.position.z);

        }

        if (inputState.turn != 0) {
            transform.Rotate(0, 0, turnSpeed * inputState.turn * -1);
        }
    }

}



