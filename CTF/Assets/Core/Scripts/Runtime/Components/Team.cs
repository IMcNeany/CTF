using UnityEngine;

namespace CTF
{
    public class Team : MonoBehaviour, ITeamMember
    {
        [SerializeField] private byte teamID;

        public byte TeamId => teamID;
    }
}
