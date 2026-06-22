using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public partial class PlayerShooting
{
    void EnsureBotBootstrap()
    {
        if (!EnemyBot.IsBotInstantiationData(photonView != null ? photonView.InstantiationData : null))
            return;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot == null)
            bot = gameObject.AddComponent<EnemyBot>();

        cachedEnemyBot = bot;
        hasResolvedEnemyBot = true;
        bot.InitializeFromPhotonData();
    }

    void EnsureNeutralRiderBootstrap()
    {
        if (!NeutralRiderController.IsNeutralRiderInstantiationData(photonView != null ? photonView.InstantiationData : null))
            return;

        NeutralRiderController rider = GetComponent<NeutralRiderController>();
        if (rider == null)
            rider = gameObject.AddComponent<NeutralRiderController>();

        rider.InitializeFromPhotonData();
    }
}
