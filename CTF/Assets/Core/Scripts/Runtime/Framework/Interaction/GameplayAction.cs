using System;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [Serializable]
    public enum GameplayAction
    {
        None = 0,
        Interact,
        PrimaryAction,
        Jump,
        OpenMenu
    }
}
