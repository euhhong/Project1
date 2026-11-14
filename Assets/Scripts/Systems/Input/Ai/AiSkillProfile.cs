namespace Project1.Systems
{
    /// <summary>
    /// AI가 고유 스킬(E)을 어떻게 쓰는지(스킬 ID별). 스킬 동작은 스킬 파일이 정하고, 여기는 "언제·얼마나 누를지"만 적는다.
    /// 모르는 스킬은 Default(가까운~중간 거리에서 가끔 짧게 누름).
    /// </summary>
    public readonly struct AiSkillProfile
    {
        /// <summary>쓸 거리(m).</summary>
        public readonly float MinRange, MaxRange;
        /// <summary>근접 교전에서 고르는 가중치(다른 공격과 견줌). 0이면 교전 중 고르지 않는다.</summary>
        public readonly float Weight;
        /// <summary>0보다 크면 E를 이만큼(초) 꾹 눌렀다 뗀다(로켓 돌진 들고 다니기, 그랩 조준).</summary>
        public readonly float HoldSeconds;
        /// <summary>돌진형(전위 럭비공·광대 로켓): 사거리 끝에서 상대 공격을 유도하고 틈에 박는다(AiChargeBaiter).</summary>
        public readonly bool Charge;
        /// <summary>0보다 크면 시전한 지 이만큼(초) 지나 E를 한 번 더 누른다(손전등 넣기).</summary>
        public readonly float RepressAfter;
        /// <summary>도약 내려찍기(루키노): 공중에서 목표 위에 오면 공격 키.</summary>
        public readonly bool AirSlam;
        /// <summary>자가치료: 체력이 줄었고 안전할 때만.</summary>
        public readonly bool SelfHeal;
        /// <summary>켜 두는 스킬(용병 아대): 꺼져 있으면 한 번 켠다.</summary>
        public readonly bool Toggle;

        private AiSkillProfile(float min, float max, float weight, float hold = 0f, bool charge = false, float repress = 0f,
            bool airSlam = false, bool selfHeal = false, bool toggle = false)
        {
            MinRange = min; MaxRange = max; Weight = weight; HoldSeconds = hold; Charge = charge; RepressAfter = repress;
            AirSlam = airSlam; SelfHeal = selfHeal; Toggle = toggle;
        }

        public static readonly AiSkillProfile Default = new(0f, 6f, 0.6f);

        public static AiSkillProfile For(string skillId) => skillId switch
        {
            "Forward_Charge" => new(1.5f, 10f, 0f, charge: true),
            "SmileyFace_RocketCharge" => new(1.5f, 10f, 0f, hold: 0.35f, charge: true),
            "Doctor_SelfHeal" => new(0f, 99f, 0f, selfHeal: true),
            "Luchino_Leap" => new(5f, 13f, 0.8f, airSlam: true),
            "Gamekeeper_Grab" => new(2.5f, 9f, 1.1f, hold: 0.15f),
            "Mercenary_Guard" => new(0f, 99f, 0f, toggle: true),
            "Geisha_Butterfly" => new(5f, 20f, 0.4f),
            "Coordinator_Flare" => new(2.5f, 12f, 1f),
            "Ripper_MistBlade" => new(2.5f, 9f, 1f),
            "Thief_Flashlight" => new(1.5f, 7f, 0.9f, repress: 2.5f),
            "HellEmber_Clone" => new(0f, 15f, 0.7f),
            "MindsEye_Thrust" => new(0.8f, 3f, 1.2f),
            _ => Default,
        };
    }
}
