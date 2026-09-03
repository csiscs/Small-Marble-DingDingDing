using System.Collections.Generic;
using UnityEngine;

public class MarbleCoinPoolSensor : MonoBehaviour
{
    private readonly HashSet<GameObject> coins = new HashSet<GameObject>();

    public int Count
    {
        get { return coins.Count; }
    }

    private void LateUpdate()
    {
        coins.RemoveWhere(coin => coin == null || !coin.activeInHierarchy);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Coin"))
            coins.Add(other.gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        coins.Remove(other.gameObject);
    }

    public void Remove(GameObject coin)
    {
        coins.Remove(coin);
    }

    public bool TryTakeRandom(out GameObject coin)
    {
        coin = null;
        if (coins.Count == 0)
            return false;

        int skip = Random.Range(0, coins.Count);
        foreach (GameObject item in coins)
        {
            if (skip-- > 0)
                continue;
            coin = item;
            break;
        }

        if (coin != null)
            coins.Remove(coin);
        return coin != null;
    }

    public bool TryTakeOne(out GameObject coin)
    {
        coin = null;
        foreach (GameObject item in coins)
        {
            coin = item;
            break;
        }

        if (coin != null)
            coins.Remove(coin);
        return coin != null;
    }
}
