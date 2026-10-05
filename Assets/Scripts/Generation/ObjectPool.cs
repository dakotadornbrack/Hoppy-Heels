using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple GameObject pool. Assign a prefab and an initial size in the Inspector.
/// Call Get() to borrow an object, Return() to give it back.
/// </summary>
public class ObjectPool : MonoBehaviour
{
    [SerializeField] GameObject prefab;
    [SerializeField] int initialSize = 15;

    readonly Queue<GameObject> available = new Queue<GameObject>();

    void Awake()
    {
        for (int i = 0; i < initialSize; i++)
            available.Enqueue(CreateNew());
    }

    GameObject CreateNew()
    {
        var go = Instantiate(prefab, transform);
        go.SetActive(false);
        return go;
    }

    /// <summary>Fetch an object from the pool, positioned at <paramref name="position"/>.</summary>
    public GameObject Get(Vector3 position)
    {
        var go = available.Count > 0 ? available.Dequeue() : CreateNew();
        go.transform.SetParent(transform);
        go.transform.position = position;
        go.GetComponent<PlatformBase>()?.ResetPlatform();
        go.SetActive(true);
        return go;
    }

    /// <summary>Return an object to the pool so it can be reused.</summary>
    public void Return(GameObject go)
    {
        go.SetActive(false);
        go.transform.SetParent(transform);
        available.Enqueue(go);
    }
}
