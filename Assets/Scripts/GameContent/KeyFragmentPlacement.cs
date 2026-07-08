using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class KeyFragment : MonoBehaviour
{
    public MazeGeneratorAlg mazeGenerator;
    public static int TotalFragments; // Общее количество фрагментов ключа
    public static int CollectedFragments = 0; // Собрано фрагментов

    public Text fragmentCounterText; // UI для отображения количества фрагментов
    
    void Start()
    {
        TotalFragments = mazeGenerator.RoomCount;
    }

    // Событие при сборе фрагмента
    public void CollectFragment()
    {
        CollectedFragments++;
        UpdateFragmentCounter();
        Destroy(gameObject); // Уничтожаем фрагмент после сбора
    }

    // Обновляем UI счётчика фрагментов
    private void UpdateFragmentCounter()
    {
        fragmentCounterText.text = $"Fragments: {CollectedFragments}/{TotalFragments}";
    }
}
