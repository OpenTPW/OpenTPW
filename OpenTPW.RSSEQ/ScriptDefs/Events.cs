﻿// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable IdentifierTypo

namespace OpenTPW.RSSEQ
{
    // duplicates here are because this file is essentially a
    // merged version of all of the event defs for each theme
    enum Events
    {
        EVT_PLANE_START = 1,
        EVT_UI_GATEOPEN = 1,
        EVT_SPIDERDOWN = 2,
        EVT_UI_GATECLOSENORMAL = 2,
        EVT_EDOOR1 = 5,
        EVT_BUBBLEE1 = 6,
        EVT_BUS = 6,
        EVT_RIDE_APE = 8,
        EVT_BUZZ3 = 13,
        EVT_SPIDERUP = 17,
        EVT_BUMPER_CAR_ENGINES = 20,
        EVT_BUMPER_CAR_FX = 21,
        EVT_BANG = 24,
        EVT_RIDEBG1 = 35,
        EVT_WHIRL02 = 38,
        EVT_BREAKDOWN = 43,
        EVT_PLANE = 46,
        EVT_BOG1 = 48,
        EVT_BOG2 = 49,
        EVT_BOG3 = 50,
        EVT_BOG4 = 51,
        EVT_BOG5 = 52,
        EVT_DOORSKWEEK1 = 53,
        EVT_PULSE3 = 54,
        EVT_ROTOR1 = 54,
        EVT_DEMON1 = 62,
        EVT_RIDE_APEGRUNT = 62,
        EVT_PULSE1 = 62,
        EVT_DEMON2 = 63,
        EVT_WINDMILL2 = 63,
        EVT_LOOP1 = 63,
        EVT_SUCK1 = 64,
        EVT_DEMON3 = 64,
        EVT_DEMON4 = 65,
        EVT_SKEL1 = 66,
        EVT_PULSE2 = 66,
        EVT_WOODLAND1 = 67,
        EVT_SKEL2 = 67,
        EVT_WOODLAND2 = 68,
        EVT_TENT1 = 68,
        EVT_TENT2 = 69,
        EVT_MOTOR1 = 69,
        EVT_WOODLAND3 = 69,
        EVT_FLAMINGO1 = 70,
        EVT_CLAMP1 = 70,
        EVT_TENT3 = 70,
        EVT_RANDOM1 = 71,
        EVT_TENT4 = 71,
        EVT_WHIRL01 = 71,
        EVT_FLAMINGO2 = 71,
        EVT_APPLE1 = 72,
        EVT_SPRING1 = 72,
        EVT_TOTEM1 = 73,
        EVT_SERVOX1 = 73,
        EVT_SERVOX2 = 74,
        EVT_TOTEM2 = 74,
        EVT_TUBE1 = 75,
        EVT_TOTEM3 = 75,
        EVT_FOUNT1 = 75,
        EVT_FORTUNE1 = 75,
        EVT_TOTEM4 = 76,
        EVT_GYRO1 = 76,
        EVT_FORTUNE2 = 76,
        EVT_GYRO2 = 77,
        EVT_TOTEM5 = 77,
        EVT_BRAIN1 = 77,
        EVT_TOTEM6 = 78,
        EVT_ROBOTURN = 78,
        EVT_ACORN2 = 78,
        EVT_DEMON5 = 79,
        EVT_ACORN3 = 79,
        EVT_ROBOFF = 79,
        EVT_ROBSWING1 = 80,
        EVT_FLAMINGO3 = 80,
        EVT_SPIDER2 = 81,
        EVT_PUFF1 = 81,
        EVT_FLAMINGO4 = 81,
        EVT_CANDLE1 = 82,
        EVT_APESWING1 = 82,
        EVT_SPRING2 = 82,
        EVT_FLAMINGO5 = 82,
        EVT_WINDMILL3 = 83,
        EVT_CANDLE2 = 83,
        EVT_APESWING2 = 83,
        EVT_ROBOTUP = 83,
        EVT_WITCH = 84,
        EVT_APESWING3 = 84,
        EVT_ROBOTDOWN = 84,
        EVT_FLOWER1 = 84,
        EVT_APESWING4 = 85,
        EVT_CANDLE3 = 85,
        EVT_CLAMP2 = 85,
        EVT_FLOWER2 = 85,
        EVT_SPIDER3 = 86,
        EVT_APESWING5 = 86,
        EVT_FLOWER3 = 86,
        EVT_ROBOTHUM = 86,
        EVT_APESWING6 = 87,
        EVT_CLAMP3 = 87,
        EVT_APESWING7 = 88,
        EVT_CLAMP4 = 88,
        EVT_CLAMP5 = 89,
        EVT_APEGRUNT1 = 89,
        EVT_TOTEM8 = 90,
        EVT_ZOB1 = 90,
        EVT_ZOB2 = 91,
        EVT_HAUNT = 91,
        EVT_ZOB3 = 92,
        EVT_BIN = 93,
        EVT_MUMBO1 = 93,
        EVT_ZOB4 = 93,
        EVT_MUMBO2 = 94,
        EVT_MUMBO3 = 95,
        EVT_DRAGON1 = 95,
        EVT_GOO1 = 95,
        EVT_MUMBO4 = 96,
        EVT_BUBBLES = 96,
        EVT_DRAGON2 = 96,
        EVT_WETFEET1 = 97,
        EVT_SPIN1 = 97,
        EVT_WETVOX = 98,
        EVT_SPIN2 = 98,
        EVT_LASER = 99,
        EVT_VOLCANO1 = 100,
        EVT_SPIN3 = 100,
        EVT_BUGS = 101,
        EVT_VOLCANO2 = 101,
        EVT_VOLCANO3 = 102,
        EVT_ROKIT1 = 102,
        EVT_ROKIT2 = 103,
        EVT_VOLCANO4 = 103,
        EVT_ARCADE = 104,
        EVT_VOLCANO5 = 104,
        EVT_WELLWIND = 104,
        EVT_ROKIT3 = 104,
        EVT_ROKIT4 = 105,
        EVT_WELLSQWK = 105,
        EVT_SNAKE1 = 106,
        EVT_CONVEY1 = 107,
        EVT_CONVEY2 = 108,
        EVT_PORK1 = 108,
        EVT_DRAC = 108,
        EVT_FRUIT = 108,
        EVT_PORK2 = 109,
        EVT_BEE = 109,
        EVT_BEAM1 = 109,
        EVT_SNAKE2 = 110,
        EVT_DUCK = 110,
        EVT_SNAKE3 = 111,
        EVT_FROG = 111,
        EVT_FISH = 112,
        EVT_SNAKE4 = 112,
        EVT_EDOOR2 = 113,
        EVT_CANDLEUP = 114,
        EVT_EFERRIS1 = 115,
        EVT_VOLCANO6 = 116,
        EVT_TEETH1 = 116,
        EVT_EFERRIS2 = 116,
        EVT_SUNGOD2 = 117,
        EVT_BUZZ1 = 117,
        EVT_BUZZ2 = 118,
        EVT_SUNGOD1 = 118,
        EVT_SUNGOD3 = 119,
        EVT_DEVIL = 119,
        EVT_GRAVE1 = 120,
        EVT_BUZZ4 = 120,
        EVT_GRAVE2 = 121,
        EVT_ROKIT5 = 121,
        EVT_JAWDROP2 = 123,
        EVT_WORM1 = 123,
        EVT_CLUNK = 124,
        EVT_GRIND1 = 125,
        EVT_PORK3 = 125,
        EVT_STEAM = 126,
        EVT_VOLCANO7 = 126,
        EVT_ROKIT6 = 127,
        EVT_GRIND3 = 127,
        EVT_VOLCANO8 = 129,
        EVT_FRUIT2 = 130,
        EVT_CRACKLES = 130,
        EVT_BELL1 = 131,
        EVT_FRUIT3 = 131,
        EVT_OBSERVE1 = 131,
        EVT_OBSERVE2 = 132,
        EVT_OBSERVE3 = 133,
        EVT_VOMIT1 = 132,
        EVT_VOMIT2 = 133,
        EVT_ZOBSTEAM = 133,
        EVT_ZGIDLE = 134,
        EVT_BUG1 = 136,
        EVT_RAT1 = 137,
        EVT_SAUCER1 = 138,
        EVT_RAT2 = 138,
        EVT_ZGTURBINE1 = 140,
        EVT_GHOSTWTR = 140,
        EVT_ZGTURBINE2 = 141,
        EVT_ZGTURBINE3 = 142,
        EVT_WIN = 144,
        EVT_GULP = 148,
        EVT_SUNGODEND = 149,
        EVT_TVUP = 150,
        EVT_TVDOWN = 151,
        EVT_JIMSPACE = 152,
        EVT_OBSERVE4 = 153,
        EVT_H_SPRAY1 = 164,
        EVT_LAVAFOUNT = 168,
        EVT_BOAT = 172,
        EVT_BIRD = 174,
        EVT_ELEPHNT = 175,
        EVT_LION = 176,
        EVT_F_SPEAKER_1 = 177,
        EVT_WELLDROP = 181,
        EVT_BELLYBOUNCE_DEFLATE = 183,
        EVT_SPARE1 = 184,
        EVT_F_TOUR = 184,
        EVT_F_FOUNT2 = 186,
        EVT_EVERYTHING_RESEARCHED = 186,
        EVT_F_SPRAY2 = 188,
        EVT_STAFF_ROOM = 188,
        EVT_BUGUP = 188,
        EVT_EXPLOS = 189,
        EVT_BUGDOWN = 189,
        EVT_PHANTOM = 190,
        EVT_F_SPRAY1 = 190,
        EVT_ROKITDOWN = 191,
        EVT_LASER_FIRE = 192,
        EVT_LASER_EXPLODE = 193,
        EVT_WATER = 194,
        EVT_LOC_NULL = 195,
        EVT_FIREWORK_EXPLODE = 199,
        EVT_FIREWORK1 = 200,
        EVT_FIREWORK2 = 201,
        EVT_FIREWORK_MORTAR = 202,
        EVT_S_SPEAKER1 = 206,
        EVT_S_SPEAKER2 = 207,
        EVT_S_SPEAKER3 = 208,
        EVT_H_SPEAKER1 = 208,
        EVT_S_SPEAKER4 = 209,
        EVT_H_SPEAKER2 = 209,
        EVT_PLANE2 = 209,
        EVT_S_SPIN1 = 210,
        EVT_H_SPEAKER3 = 210,
        EVT_H_SPEAKER4 = 211,
        EVT_SIDESHOW_WIN = 212,
        EVT_APE_CRUNCH = 219,
        EVT_APE_THUMP = 220,
        EVT_CYCLONE01 = 222,
        EVT_CYCLONE02 = 223,
        EVT_J_SPEAKER1 = 236,
        EVT_F_SPEAKER_2 = 237,
        EVT_F_SPEAKER_3 = 238,
        EVT_F_SPEAKER_4 = 239,
        EVT_J_FOUNT2 = 244,
        EVT_FOUNTAIN = 245,
        EVT_J_SPRAY2 = 246,
        EVT_J_SPRAY1 = 247,
        EVT_KINGSWING01 = 254,
        EVT_MANICMAYANCLICK01 = 260,
        EVT_HOTPOTWTR = 297
    }
}
