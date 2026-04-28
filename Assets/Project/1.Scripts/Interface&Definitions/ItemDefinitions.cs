using UnityEngine;

public interface IInteractable // 상호작용 인터페이스.
{
    public void Interact();
}

interface IItem
{
    //public GetGrade();
}

interface IWeapon // Weapon인터페이스
{
    public void SetWeaponCollider(string command);
    public void SetWeaponCollider(bool enabled);
    public void SetWeaponCollider(int value);
    public void GetDamage();
}
