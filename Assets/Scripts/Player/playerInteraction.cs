using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionRadius = 1.5f; // Радиус проверки для взаимодействия
    public LayerMask interactableLayer; // Слой для объектов, с которыми можно взаимодействовать

    private void FixedUpdate()
    {
        if (Input.GetKeyDown(KeyCode.E)) // Нажатие на кнопку E
        {
            Interact();
        }
    }

    void Interact()
    {
        Collider2D[] nearbyObjects = Physics2D.OverlapCircleAll(transform.position, interactionRadius, interactableLayer);

        foreach (var obj in nearbyObjects)
        {
            // Проверяем, с каким объектом игрок может взаимодействовать
            if (obj.CompareTag("KeyFragment"))
            {
                obj.GetComponent<KeyFragment>().CollectFragment(); // Собираем фрагмент
                return;
            }
            else if (obj.CompareTag("Exit") && KeyFragment.CollectedFragments == KeyFragment.TotalFragments)
            {
                // Если игрок собрал все фрагменты, выход становится доступен
                ExitMaze();
                return;
            }
            else
            {
                // Если не можем взаимодействовать с объектом
                ShowInteractionMessage("Interaction not possible.");
                return;
            }
        }
    }

    // Функция для выхода из игры (или перехода на новый уровень)
    void ExitMaze()
    {
        // Логика выхода
        Debug.Log("You have exited the maze!");
    }

    void ShowInteractionMessage(string message)
    {
        // Показать сообщение на экране
        Debug.Log(message); // Это можно заменить на UI-сообщение
    }
}
