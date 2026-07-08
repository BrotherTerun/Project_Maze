using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEditor.PlayerSettings;

public class MazeGeneratorAlg : MonoBehaviour
{
    public int width = 101; // Должно быть нечётным числом
    public int height = 101; // Должно быть нечётным числом
    public int RoomCount = 4;
    public int RoomSize = 15;

    public Tilemap WallTilemap;
    public Tilemap FloorTilemap;
    public TileBase wallTile;
    public TileBase floorTile;
    public TileBase checkTile;


    public float wallClusterChance = 0.05f; // Шанс появления кластера
    public int WallClusterSize = 4; // Количество тайлов в кластере

    public float widenChance = 0.15f;

    readonly private List<Vector2Int> frontierCells = new();
    readonly private HashSet<Vector2Int> mazeCells = new();

    private readonly Vector2Int[] directions = new []
    {
        new Vector2Int(2, 0),  // Вправо
        new Vector2Int(-2, 0), // Влево
        new Vector2Int(0, 2),  // Вверх
        new Vector2Int(0, -2)  // Вниз
    };

    public Player player;
    public GameObject Exit;

    // Start is called before the first frame update
    void Start()
    {
        // Очистка старого лабиринта
        WallTilemap.ClearAllTiles();
        FloorTilemap.ClearAllTiles();

        // 1. Создаём центральную комнату
        int roomWidth = Mathf.Max(1, width / 20);
        int roomHeight = Mathf.Max(1, height / 20);
        int centerX = width / 2 - roomWidth / 2;
        int centerY = height / 2 - roomHeight / 2;

        Vector3Int spawnPosition = new(centerX, centerY, 0);

        // Очищаем место для комнаты
        for (int dx = 0; dx < roomWidth; dx++)
        {
            for (int dy = 0; dy < roomHeight; dy++)
            {
                Vector3Int pos = new(centerX + dx, centerY + dy, 0);
                FloorTilemap.SetTile(pos, floorTile);
                WallTilemap.SetTile(pos, null);
            }
        }

        // 2. Спавн игрока в центре комнаты
        player.transform.position = spawnPosition + new Vector3(0.5f, 0.5f, 0);

        GenerateRooms(RoomCount, RoomSize); 
        
        // Заполняем всё кроме комнат стенами
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (FloorTilemap.GetTile(new Vector3Int(x, y, 0)) != floorTile)
                {
                    WallTilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
                }
                    
            }
        }

        GenerateMaze();
        CreateWallClusters();
        WidenRandomCorridors();
        SetRoomWalls(spawnPosition);
        DeleteSingleWalls();
        PlaceSpawnAndExit(spawnPosition);
        EnsurePathInAllSectors();
        SetWalls(spawnPosition);
        SetFloor();
        
    }

    void SetFloor()
    {
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                Vector3Int pos = new(j, i, 0);
                if (FloorTilemap.GetTile(pos) == null && WallTilemap.GetTile(pos) == null)
                {
                    FloorTilemap.SetTile(pos, floorTile); 
                }
            }
        }
    }

    void SetWalls(Vector3Int spawnPosition)
    {
        HashSet<Vector3Int> reachableTiles = new();
        Queue<Vector3Int> queue = new();

        queue.Enqueue(spawnPosition);
        reachableTiles.Add(spawnPosition);

        // BFS для поиска всех доступных клеток
        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();

            foreach (Vector3Int direction in new Vector3Int[]
            {
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0)
            })
            {
                Vector3Int neighbor = current + direction;

                // Проверяем, чтобы не выйти за границы карты
                if (neighbor.x < 0 || neighbor.x >= width || neighbor.y < 0 || neighbor.y >= height)
                    continue;

                // Если клетка уже посещена, пропускаем
                if (reachableTiles.Contains(neighbor))
                    continue;

                // Теперь проверяем, является ли клетка полом
                if (WallTilemap.GetTile(neighbor) == null)
                {
                    reachableTiles.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        // Заполняем стеными все недоступные клетки
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                Vector3Int pos = new(j, i, 0);

                // **Если клетка НЕ доступна и НЕ является стеной — превращаем её в стену**
                if (!reachableTiles.Contains(pos) && FloorTilemap.GetTile(pos) == null && WallTilemap.GetTile(pos) == null)
                {
                    WallTilemap.SetTile(pos, checkTile);
                }
            }
        }
    }

    void GenerateRooms(int RoomCount, int roomSize)
    {
        int gridSizeX = Mathf.FloorToInt(width / 30); // Количество секторов по X
        int gridSizeY = Mathf.FloorToInt(width / 30); // Количество секторов по Y

        int sectorWidth = width / gridSizeX;  // Размер сектора по X
        int sectorHeight = height / gridSizeY; // Размер сектора по Y

        List<RectInt> placedRooms = new(); // Список уже размещённых комнат
        List<Vector2Int> availableSectors = new();

        // Заполняем список доступных секторов
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                availableSectors.Add(new Vector2Int(x, y));
            }
        }

        // Вычисляем центральный сектор
        int centerX = gridSizeX / 2;
        int centerY = gridSizeY / 2;

        // Удаляем центральный сектор из доступных
        availableSectors.Remove(new Vector2Int(centerX, centerY));

        // Перемешиваем список секторов, чтобы распределение было случайным
        ShuffleList(availableSectors);

        for (int i = 0; i < RoomCount && availableSectors.Count > 0; i++)
        {
            Vector2Int sector = availableSectors[0]; // Берем случайный сектор
            availableSectors.RemoveAt(0); // Удаляем его из доступных

            int RoomWidth = roomSize;
            int RoomHeight = roomSize;

            // Ограничиваем координаты комнаты так, чтобы она не выходила за границы сектора
            int minX = sector.x * sectorWidth + 1;
            int maxX = Mathf.Min((sector.x + 1) * sectorWidth - RoomWidth - 1, width - RoomWidth - 1);
            int minY = sector.y * sectorHeight + 1;
            int maxY = Mathf.Min((sector.y + 1) * sectorHeight - RoomHeight - 1, height - RoomHeight - 1);

            int x = Random.Range(minX, maxX);
            int y = Random.Range(minY, maxY);

            RectInt newRoom = new(x, y, RoomWidth, RoomHeight);
            placedRooms.Add(newRoom);

            // Заполняем тайлы пола
            for (int dx = 0; dx < RoomWidth; dx++)
            {
                for (int dy = 0; dy < RoomHeight; dy++)
                {
                    Vector3Int pos = new(x + dx, y + dy, 0);
                    FloorTilemap.SetTile(pos, floorTile);
                }
            }
        }
    }

    // Перемешивание списка (Фишер-Йетс)
    void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    bool IsEmptyFloor(Vector3Int pos)
    {
        return FloorTilemap.GetTile(pos) == null;
    }

    void GenerateMaze()
    {
        // Выбираем стартовую точку в незанятой области
        Vector3Int start = new(Random.Range(0, width / 2) * 2 + 1, Random.Range(0, height / 2) * 2 + 1, 0);
        while (FloorTilemap.GetTile(start) != null)
        {
            start = new Vector3Int(Random.Range(0, width / 2) * 2 + 1, Random.Range(0, height / 2) * 2 + 1, 0);
        }
        WallTilemap.SetTile(start, null);
        AddToMaze((Vector2Int)start);

        while (frontierCells.Count > 0)
        {
            // Выбираем случайную границу
            Vector2Int current = frontierCells[Random.Range(0, frontierCells.Count)];
            frontierCells.Remove(current);

            // Проверяем, есть ли у неё сосед, который уже в лабиринте
            List<Vector2Int> neighbors = GetMazeNeighbors(current);

            if (neighbors.Count > 0)
            {
                // Выбираем случайного соседа
                Vector2Int chosenNeighbor = neighbors[Random.Range(0, neighbors.Count)];

                // Удаляем стену между ними
                Vector2Int wall = (current + chosenNeighbor) / 2;
                WallTilemap.SetTile(new Vector3Int(wall.x, wall.y, 0), null);

                // Добавляем текущую клетку в лабиринт
                AddToMaze(current);
            }
        }
    }

    void AddToMaze(Vector2Int pos)
    {
        mazeCells.Add(pos);
        WallTilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), null);

        // Добавляем соседей в границу
        foreach (var dir in directions)
        {
            Vector2Int neighbor = pos + dir;
            if (IsInBounds(neighbor) && !mazeCells.Contains(neighbor) && !frontierCells.Contains(neighbor))
            {
                frontierCells.Add(neighbor);
            }
        }
    }

    bool IsInBounds(Vector2Int pos)
    {
        return pos.x > 0 && pos.x < width - 1 && pos.y > 0 && pos.y < height - 1;
    }

    List<Vector2Int> GetMazeNeighbors(Vector2Int pos)
    {
        List<Vector2Int> neighbors = new();

        foreach (var dir in directions)
        {
            Vector2Int neighbor = pos + dir;
            if (mazeCells.Contains(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    void CreateWallClusters()
    {
        List<Vector3Int> emptyPositions = new();

        // Собираем все позиции пола
        BoundsInt bounds = WallTilemap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (WallTilemap.GetTile(pos) == null && IsInBounds((Vector2Int)pos) && FloorTilemap.GetTile(pos) == null) // Проверяем, что не граница
            {
                emptyPositions.Add(pos);
            }
        }

        // Перебираем случайные клетки пола и создаем кластеры стен
        foreach (Vector3Int pos in emptyPositions)
        {
            if (Random.value < wallClusterChance) // С вероятностью создаем кластер
            {
                CreateWallCluster(pos);
            }
        }
    }

    void CreateWallCluster(Vector3Int startPos)
    {
        Queue<Vector3Int> queue = new();
        HashSet<Vector3Int> visited = new();

        queue.Enqueue(startPos);
        visited.Add(startPos);

        int placedCount = 0;

        while (queue.Count > 0 && placedCount < WallClusterSize)
        {
            Vector3Int current = queue.Dequeue();
            WallTilemap.SetTile(current, wallTile); // Добавляем стену
            placedCount++;

            // Добавляем соседей случайным образом
            List<Vector3Int> neighbors = GetNeighbors(current);
            foreach (Vector3Int neighbor in neighbors)
            {
                if (!visited.Contains(neighbor) && WallTilemap.GetTile(neighbor) == null && IsInBounds((Vector2Int)neighbor))
                {
                    queue.Enqueue(neighbor);
                    visited.Add(neighbor);
                }
            }
        }
    }
    List<Vector3Int> GetNeighbors(Vector3Int pos)
    {
        return new List<Vector3Int>
        {
            new(pos.x + 1, pos.y, pos.z),
            new(pos.x - 1, pos.y, pos.z),
            new(pos.x, pos.y + 1, pos.z),
            new(pos.x, pos.y - 1, pos.z)
        };
    }

    void WidenRandomCorridors()
    {
        BoundsInt bounds = WallTilemap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (IsEmptyFloor(pos) && IsInBounds((Vector2Int)pos) && Random.value < widenChance)
            {
                ExpandTile(pos);
            }
        }
    }

    void ExpandTile(Vector3Int pos)
    {
        List<Vector3Int> neighbors = GetNeighbors(pos);
        foreach (Vector3Int neighbor in neighbors)
        {
            if (WallTilemap.GetTile(neighbor) == wallTile && IsInBounds((Vector2Int)neighbor))
            {
                WallTilemap.SetTile(neighbor, null); // Превращаем стену в пол
            }
        }
    }

    void SetRoomWalls(Vector3Int spawnPosition)
    {
        List <Vector3Int> RoomTiles = new();
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                Vector3Int pos = new(j, i, 0);
                if (FloorTilemap.GetTile(pos) == floorTile)
                {
                    RoomTiles.Add(pos);
                    foreach (Vector3Int neighbor in GetNeighbors(pos))
                    {
                        if (WallTilemap.GetTile(neighbor) != wallTile && FloorTilemap.GetTile(neighbor) != floorTile)
                        {
                            WallTilemap.SetTile(neighbor, wallTile);
                        }
                    }
                }
            }
        }
        foreach (Vector3Int tile in RoomTiles)
        {
            //Находим путь с учетом разрушения стен
            List<Vector3Int> path = AStarPathfinding.FindPath(spawnPosition, tile, FloorTilemap, WallTilemap);
            if (path != null)
            {
                // Разрушаем только стены на пути
                foreach (Vector3Int posi in path)
                {
                    if (WallTilemap.GetTile(posi) != null) // Если это стена, удаляем её
                    {
                        WallTilemap.SetTile(posi, null);
                    }
                }
            }
            else
            {
                Debug.LogWarning("Путь до комнаты не найден!");
            }
        }
    }

    void DeleteSingleWalls()
    {
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                if (WallTilemap.GetTile(new Vector3Int(j, i, 0)) == wallTile)
                {
                    if (WallTilemap.GetTile(new Vector3Int(j + 1, i, 0)) == null && WallTilemap.GetTile(new Vector3Int(j - 1, i, 0)) == null && WallTilemap.GetTile(new Vector3Int(j, i + 1, 0)) == null && WallTilemap.GetTile(new Vector3Int(j, i - 1, 0)) == null && WallTilemap.GetTile(new Vector3Int(j + 1, i + 1, 0)) == null && WallTilemap.GetTile(new Vector3Int(j + 1, i - 1, 0)) == null && WallTilemap.GetTile(new Vector3Int(j - 1, i + 1, 0)) == null && WallTilemap.GetTile(new Vector3Int(j - 1, i - 1, 0)) == null)
                    {
                        WallTilemap.SetTile(new Vector3Int(j, i, 0), null);
                    }
                }
            }
        }
    }

    void PlaceSpawnAndExit(Vector3Int spawnPosition)
    {
        // 3. Выбираем случайную границу для выхода
        List<Vector3Int> exitCandidates = new();

        for (int x = 1; x < width - 1; x++)
        {
            exitCandidates.Add(new Vector3Int(x, 0, 0)); // Нижняя граница
            exitCandidates.Add(new Vector3Int(x, height - 1, 0)); // Верхняя граница
        }
        for (int y = 1; y < height - 1; y++)
        {
            exitCandidates.Add(new Vector3Int(0, y, 0)); // Левая граница
            exitCandidates.Add(new Vector3Int(width - 1, y, 0)); // Правая граница
        }

        Vector3Int exitPosition = exitCandidates[Random.Range(0, exitCandidates.Count)];

        // 4. Размещаем выход
        FloorTilemap.SetTile(exitPosition, floorTile);
        WallTilemap.SetTile(exitPosition, null);
        Exit.transform.position = exitPosition;

        // 5. Находим путь с учетом разрушения стен
        List<Vector3Int> path = AStarPathfinding.FindPath(spawnPosition, exitPosition, FloorTilemap, WallTilemap);

        if (path != null)
        {
            // Разрушаем только стены на пути
            foreach (Vector3Int pos in path)
            {
                if (WallTilemap.GetTile(pos) != null) // Если это стена, удаляем её
                {
                    WallTilemap.SetTile(pos, null);
                    FloorTilemap.SetTile(pos, floorTile);
                }
            }
        }
        else
        {
            Debug.LogWarning("Путь не найден!");
        }
    }

    void EnsurePathInAllSectors()
    {
        int gridSizeX = Mathf.FloorToInt(width / 30); // Количество секторов по X
        int gridSizeY = Mathf.FloorToInt(width / 30); // Количество секторов по Y

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector2Int sector = new(x, y);
                Vector3Int randomEmptyTile = GetRandomEmptyTileInSector(sector); // Случайная пустая клетка в секторе

                if (randomEmptyTile != Vector3Int.zero) // Если есть хотя бы один пустой тайл
                {
                    // Проверяем, есть ли путь из текущей точки в случайную пустую клетку
                    Vector3Int start = new(sector.x * width / gridSizeX, sector.y * height / gridSizeY, 0); // Начало (например, верхний левый угол сектора)

                    List<Vector3Int> path = AStarPathfinding.FindPath(start, randomEmptyTile, FloorTilemap, WallTilemap);

                    if (path == null || path.Count == 0) // Путь не найден — разрушим стену
                    {
                        Debug.LogWarning("Путь не найден в секторе " + sector + ". Разрушение стены...");

                        // Здесь логика для разрушения стен, если путь не найден
                        DestroyWallsToCreatePath(start, randomEmptyTile);
                    }
                }
            }
        }
    }

    Vector3Int GetRandomEmptyTileInSector(Vector2Int sector)
    {
        int sectorWidth = width / Mathf.FloorToInt(width / 30);
        int sectorHeight = height / Mathf.FloorToInt(height / 30);

        for (int x = sector.x * sectorWidth; x < (sector.x + 1) * sectorWidth; x++)
        {
            for (int y = sector.y * sectorHeight; y < (sector.y + 1) * sectorHeight; y++)
            {
                Vector3Int position = new(x, y, 0);
                if (FloorTilemap.GetTile(position) == null && WallTilemap.GetTile(position) == null) // Если клетка является пустой
                {
                    return position;
                }
            }
        }
        return Vector3Int.zero; // Если нет пустых клеток
    }

    void DestroyWallsToCreatePath(Vector3Int start, Vector3Int end)
    {
        // Здесь мы будем разрушать стены по пути
        List<Vector3Int> path = AStarPathfinding.FindPath(start, end, FloorTilemap, WallTilemap);

        if (path != null)
        {
            foreach (Vector3Int position in path)
            {
                if (WallTilemap.GetTile(position) != null) // Если на этом пути стена
                {
                    WallTilemap.SetTile(position, null); // Удаляем стену
                }
            }
        }
    }
}
