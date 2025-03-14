//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

public class RCC_InitLoadHappyMessage {

    [InitializeOnLoadMethod]
    public static void InitOnLoad() {

        if (SessionState.GetBool("BCG_HAPPYMESSAGE", false))
            return;

        EditorApplication.delayCall += () => {

            string[] messages = new string[10];

            messages[0] = "BoneCracker Games | Thank you for choosing our assets. We hope they enhance your project and bring your vision to life!";
            messages[1] = "BoneCracker Games | We're thrilled to have you using our assets. If you need any assistance, feel free to reach out. Happy developing!";
            messages[2] = "BoneCracker Games | We appreciate your purchase. Dive in and let our assets help you create something amazing!";
            messages[3] = "BoneCracker Games | Thanks for your support. We’re excited to see what you build with our assets. Good luck and have fun!";
            messages[4] = "BoneCracker Games | Your journey to creating fantastic projects just got easier with our assets. Enjoy and create something incredible!";
            messages[5] = "BoneCracker Games | Thank you for your purchase. We’re here to support you in making your project a success. Happy coding!";
            messages[6] = "BoneCracker Games | We’re delighted to be part of your development journey. Let our assets spark your creativity!";
            messages[7] = "BoneCracker Games | Thanks for choosing our assets. We can’t wait to see what you’ll create. If you need help, we’re here for you!";
            messages[8] = "BoneCracker Games | We’re excited to see our assets in action in your projects. Wishing you the best in your development endeavors!";
            messages[9] = "BoneCracker Games | We appreciate your trust in our assets. Get started and bring your ideas to life. We're here to assist you along the way!";

            string randomMessage = messages[UnityEngine.Random.Range(0, 10)];

            Debug.Log("<color=#00FF00>" + randomMessage + "</color>");
            SessionState.SetBool("BCG_HAPPYMESSAGE", true);

        };

    }

}
