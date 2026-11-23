using System.Collections.Generic;
using UnityEngine;

public class RelationManager : MonoBehaviour
{

    private Dictionary<string, int> relations = new();
    private HashSet<string> alliances = new();

    public bool IsAllied(string a, string b)
    {
        return alliances.Contains(MakeKey(a, b));
    }

    public void GiveGift(string from, string to)
    {
        ChangeRelation(from, to, 10);
    }

    public bool TryMarry(string a, string b)
    {
        if (GetRelation(a, b) >= 60)
        {
            alliances.Add(MakeKey(a, b));
            return true;
        }
        return false;
    }

    private string MakeKey(string a, string b)
    {
        if (string.Compare(a, b) < 0)
            return a + "|" + b;
        return b + "|" + a;
    }

    public int GetRelation(string a, string b)
    {
        relations.TryGetValue(MakeKey(a, b), out int result);
        return result;
    }

    public void ChangeRelation(string a, string b, int amount)
    {
        int newValue = Mathf.Clamp(GetRelation(a, b) + amount, -100, 100);
        relations[MakeKey(a, b)] = newValue;
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ChangeRelation("Humans", "Ogres", 10);
        ChangeRelation("Humans", "Elves", 70);
        Debug.Log(GetRelation("Ogres", "Humans"));
        Debug.Log(TryMarry("Humans", "Ogres"));
        Debug.Log(TryMarry("Elves", "Humans"));
        Debug.Log(IsAllied("Humans", "Elves"));
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
