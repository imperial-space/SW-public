using Robust.Shared.Serialization;

/// <summary>
/// Ивент вызывается при успешном использовании ключа на сущности Lockable (двери, сундуки, якорь). У сущности компонент UniversalLockableComponent
/// NewLockState означает новое состояние замка после использования ключа (открыт/закрыт)
/// </summary>

[Serializable]
public sealed class OnLockableKeyUsedSuccessEvent : EntityEventArgs
{
    public bool NewLockState = false;
}
