using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public struct InteractionPromptState
    {
        public bool Visible;
        public string Text;
        public bool ShowProgress;
    }


    /// <summary>
    /// Event to send to the HUD to updated the interaction prompt
    /// </summary>
    [CreateAssetMenu(fileName = "InteractionPromptEvent", menuName = "Game Events/Interaction Prompt Event")]
    public class InteractionPromptEvent : GameEvent<InteractionPromptState>
    {

    }
}
