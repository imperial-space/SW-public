Boss_Encounter_Name = Boss

Boss_Encounter_Execution = Tank buster

Boss_Encounter_Title = arena guardian

Boss_Encounter_Name_Possessive = the boss’s

Boss_Encounter_Gate_Closed = The gate is sealed. Use its key to open it.

Boss_Encounter_Gate_Unavailable = You cannot enter the gate right now.

Boss_Encounter_Gate_Combat_Locked = The battle has begun. Wait for the arena to reset.

Boss_Encounter_Gate_Key_Required = Hold { Boss_Encounter_Key } and use it on the gate to open it.

Boss_Encounter_Gate_Arena_Failed = The arena could not be prepared. The gate remains sealed.

Boss_Encounter_Gate_Unlocked = The gate opens. The key is consumed.

Boss_Encounter_Victory_Loot_Time = Collect your loot and leave within { $minutes } minutes.

Boss_Encounter_Victory_Closing = The arena will close in one minute.

Boss_Encounter_Victory_Expired = Time is up. The arena has closed.

Boss_Encounter_Gate_Arrival_Blocked = The arrival point is blocked. The gate cannot transport you safely.

Boss_Encounter_Command_Bossgate_Desc = Links, opens, or closes a boss arena gate.

Boss_Encounter_Command_Bossgate_Help = Usage: bossgate link <gate ID or prototype> <arrival point ID or prototype>; bossgate open <gate>; bossgate close <gate>.

Boss_Encounter_Command_Bossgate_Select_Gate = Select exactly one arena gate by entity ID or prototype ID.

Boss_Encounter_Command_Bossgate_Select_Entrance = Select exactly one arena arrival point placed on a map.

Boss_Encounter_Command_Bossgate_Needs_Entrance = Link the gate to a valid arena arrival point before opening it.

Boss_Encounter_Command_Bossgate_Linked = Arena arrival point linked. Use { Boss_Encounter_Key } to open it, or bossgate open for an admin override.

Boss_Encounter_Command_Bossgate_Opened = Arena gate opened.

Boss_Encounter_Command_Bossgate_Closed = Arena gate closed.

Boss_Encounter_Rules_No_Building = You cannot create obstacles or change the terrain in this arena.

Boss_Encounter_Rules_No_Furniture = Leave blocking furniture outside the arena.

Boss_Encounter_Rules_Protected = This encounter cannot be moved or contained.

Boss_Encounter_Cast_Default_Name = Casting

Boss_Encounter_Cast_Preview_Name = Test cast

Boss_Encounter_Command_Bosscast_Desc = Previews or cancels a named boss cast without activating its encounter.

Boss_Encounter_Command_Bosscast_Help = Usage: bosscast <boss ID or prototype> [seconds] [ability name]; bosscast cancel <boss ID or prototype>. Defaults to a harmless 3-second test cast.

Boss_Encounter_Command_Bosscast_Select_Boss = Select exactly one boss by entity ID or prototype ID.

Boss_Encounter_Command_Bosscast_Duration = Preview duration must be between 0.1 and 120 seconds.

Boss_Encounter_Command_Bosscast_Name_Too_Long = Ability names must be at most 128 characters.

Boss_Encounter_Command_Bosscast_Unavailable = The boss must be alive, unfinished, unpaused, and not already casting.

Boss_Encounter_Command_Bosscast_Started = Harmless cast preview started.

Boss_Encounter_Command_Bosscast_Cancelled = Boss cast cancelled.

Boss_Encounter_Command_Bosscast_No_Cast = This boss is not casting.

Boss_Encounter_Command_Testboss_Desc = Starts a boss encounter with connected, living humanoid players.

Boss_Encounter_Command_Testboss_Help = Usage: testboss [boss prototype ID or entity ID]. Local encounters include players on the boss's grid; arena encounters require BossSpawnPosition markers.

Boss_Encounter_Command_Testboss_Select = Select exactly one spawned boss by prototype ID or entity ID, for example: testboss RevenantBoss.

Boss_Encounter_Command_Testboss_Start_Failed = Could not start the fight. The boss must be on a grid, inactive and unfinished, with living players available. Arena bosses also need BossSpawnPosition markers.

Boss_Encounter_Command_Testboss_Started = Started the encounter for { $boss }.

Boss_Encounter_Command_Crescentreap_Desc = Previews directional Reaps or a prepared Requiem combo without damage.

Boss_Encounter_Command_Crescentreap_Help = Usage: crescentreap <boss ID or prototype> [random|septentrio,oriens,...|cancel] [normal|slow|fast]; crescentreap <boss> <septentrio|oriens|meridies|occidens> [seconds]. Comma-separated combos require 2-4 directions. North/east/south/west and older directional aliases are also accepted. No arguments after the boss selects a random combo. All previews are harmless.

Boss_Encounter_Command_Midnightvigil_Desc = Previews Vigil without damage.

Boss_Encounter_Command_Midnightvigil_Help = Usage: midnightvigil <boss ID or prototype> [nw|ne|sw|se] [seconds]. The corner selects the safe brazier. Default: nw, 6 seconds. Harmless preview.

Boss_Encounter_Command_Blackout_Desc = Legacy alias for Vigil's harmless preview.

Boss_Encounter_Command_Blackout_Help = Usage: blackout <boss ID or prototype> [nw|ne|sw|se] [seconds]. Prefer midnightvigil. Default: nw, 6 seconds. Harmless preview.

Boss_Encounter_Command_Deathsembrace_Desc = Previews { Boss_Encounter_Deaths_Bliss } or { Boss_Encounter_Deaths_Blight } without damage.

Boss_Encounter_Command_Deathsembrace_Help = Usage: deathsembrace <boss ID or prototype> [rejection|invitation] [seconds]. Default: rejection, 6 seconds. Harmless preview.

Boss_Encounter_Command_Sepulchralcross_Desc = Previews Cross without damage.

Boss_Encounter_Command_Sepulchralcross_Help = Usage: sepulchralcross <boss ID or prototype> [seconds]. Default: 5 seconds. Harmless preview.

Boss_Encounter_Command_Sepulchraldivide_Desc = Previews Divide without damage.

Boss_Encounter_Command_Sepulchraldivide_Help = Usage: sepulchraldivide <boss ID or prototype> [seconds]. Default: 5 seconds. Harmless preview.

Boss_Encounter_Command_Reaperssentence_Desc = Previews Severance or Communion without damage.

Boss_Encounter_Command_Reaperssentence_Help = Usage: reaperssentence <boss ID or prototype> [severance|communion] [seconds]. Severance separates marked players; Communion gathers the party. Default: severance, 5 seconds. Requires living arena players. Harmless preview.

Boss_Encounter_Command_Paleconstellation_Desc = Previews Constellation without damage.

Boss_Encounter_Command_Paleconstellation_Help = Usage: paleconstellation <boss ID or prototype> [elegy|dirge] [seconds]. Elegy selects blue then purple; dirge selects purple then blue. Both casts display Constellation; players read the order from the colored flames. Default: elegy, 6 seconds. Harmless preview.

Boss_Encounter_Requiem = Release

Boss_Encounter_Sentencing = Sentencing { $attack }

Boss_Encounter_Command_Bosssequence_Desc = Previews { Boss_Encounter_Name_Possessive } prepared cleave combos without damage.

Boss_Encounter_Command_Bosssequence_Help = Usage: bosssequence <boss ID or prototype> [random|left,right,...|cancel] [normal|slow|fast]. Supply 2-4 directions: septentrio, oriens, meridies, occidens (or north, east, south, west; older aliases still work). Prefer crescentreap for mechanic-named commands. Normal: 2 seconds between hits; slow: 3; fast: 1.6. All previews are harmless. Start actual combat using testboss.

Boss_Encounter_Command_Bosssequence_Unavailable = Select { Boss_Encounter_Name } on the mapped arena, with the encounter unfinished, a configured sequence attack and no cast or sequence running.

Boss_Encounter_Command_Bosssequence_Started = Started a harmless prepared combo preview: { $count } strikes, { $interval } seconds apart. Preloads: { $preload } seconds; release: { $release } seconds.

Boss_Encounter_Command_Bosssequence_Cancelled = Cancelled the entire prepared combo and its warnings.

Boss_Encounter_Command_Bosssequence_No_Sequence = This boss has no prepared combo running.

Boss_Encounter_Command_Bosstelegraph_Desc = Previews harmless boss telegraphs and target markers.

Boss_Encounter_Command_Bosstelegraph_Help = Usage: bosstelegraph <boss ID or prototype> <septentrio|oriens|meridies|occidens|blackout-nw|blackout-ne|blackout-sw|blackout-se|rejection|invitation|cross|divide|severance|communion|elegy|dirge|crystal-small|crystal-large> [seconds]; bosstelegraph <boss> tank [player entity ID]; bosstelegraph <boss> clear. For Midnight Vigil use midnightvigil <boss> [nw|ne|sw|se] [seconds]. Older variant names remain aliases.

Boss_Encounter_Command_Bosstelegraph_Cleared = Cleared the cast preview and target marks.

Boss_Encounter_Command_Bosstelegraph_Tank_Failed = Select a living player on this boss's arena platform.

Boss_Encounter_Command_Bosstelegraph_Tank_Set = Set the main-tank mark. This does not start combat or deal damage.

Boss_Encounter_Command_Bosstelegraph_Duration = Preview duration must be between 1 and 120 seconds.

Boss_Encounter_Command_Bosstelegraph_Unavailable = Select { Boss_Encounter_Name } on the mapped arena with the encounter unfinished and no cast already running. Severance/Communion also require living arena players.

Boss_Encounter_Command_Bosstelegraph_Started = Started a harmless telegraph preview. No attack damage is applied.

Boss_Encounter_Occidens = West

Boss_Encounter_Oriens = East

Boss_Encounter_Septentrio = North

Boss_Encounter_Meridies = South

Boss_Encounter_Cruciform_Vigil = Cruciform Vigil

Boss_Encounter_Sundered_Vigil = Sundered Vigil

Boss_Encounter_Dire_Requiem = Release

Boss_Encounter_Vigil = Blackout

Boss_Encounter_Deaths_Bliss = Inner then outer

Boss_Encounter_Deaths_Blight = Outer then inner

Boss_Encounter_Cross = Cross

Boss_Encounter_Divide = Divide

Boss_Encounter_Severance = Spread

Boss_Encounter_Communion = Stack

Boss_Encounter_Constellation = Constellation

Boss_Encounter_Elegy = { Boss_Encounter_Constellation }

Boss_Encounter_Dirge = { Boss_Encounter_Constellation }

Boss_Encounter_Gate_Desc = An ancient gate leading to { Boss_Encounter_Name_Possessive } arena.

Boss_Encounter_Gate_Suffix = Closed

Boss_Encounter_Return_Flame = { Boss_Encounter_Name_Possessive } return flame
    .desc = { Boss_Encounter_Return_Flame_Desc }
    .suffix = { Boss_Encounter_Return_Flame_Suffix }

Boss_Encounter_Return_Flame_Desc = A lingering portal that returns victors to the entrance.

Boss_Encounter_Return_Flame_Suffix = { "" }

Boss_Encounter_Center = reserved arena center
    .desc = { Boss_Encounter_Center_Desc }
    .suffix = { Boss_Encounter_Center_Suffix }

Boss_Encounter_Center_Desc = Reserves the boss's position in the arena.

Boss_Encounter_Center_Suffix = { "" }

Boss_Encounter_Telegraph = attack warning
    .desc = { Boss_Encounter_Telegraph_Desc }
    .suffix = { Boss_Encounter_Telegraph_Suffix }

Boss_Encounter_Telegraph_Desc = { "" }

Boss_Encounter_Telegraph_Suffix = { "" }

Boss_Encounter_Impact = attack impact
    .desc = { Boss_Encounter_Impact_Desc }
    .suffix = { Boss_Encounter_Impact_Suffix }

Boss_Encounter_Impact_Desc = { "" }

Boss_Encounter_Impact_Suffix = { "" }

Boss_Encounter_Tank_Buster_Marker = tank buster marker
    .desc = { Boss_Encounter_Tank_Buster_Marker_Desc }
    .suffix = { Boss_Encounter_Tank_Buster_Marker_Suffix }

Boss_Encounter_Tank_Buster_Marker_Desc = { "" }

Boss_Encounter_Tank_Buster_Marker_Suffix = { "" }

Boss_Encounter_Tank_Marker = tank marker
    .desc = { Boss_Encounter_Tank_Marker_Desc }
    .suffix = { Boss_Encounter_Tank_Marker_Suffix }

Boss_Encounter_Tank_Marker_Desc = { "" }

Boss_Encounter_Tank_Marker_Suffix = { "" }

Boss_Encounter_Spread_Marker = Severance marker
    .desc = { Boss_Encounter_Spread_Marker_Desc }
    .suffix = { Boss_Encounter_Spread_Marker_Suffix }

Boss_Encounter_Spread_Marker_Desc = { "" }

Boss_Encounter_Spread_Marker_Suffix = { "" }

Boss_Encounter_Stack_Marker = Communion marker
    .desc = { Boss_Encounter_Stack_Marker_Desc }
    .suffix = { Boss_Encounter_Stack_Marker_Suffix }

Boss_Encounter_Stack_Marker_Desc = { "" }

Boss_Encounter_Stack_Marker_Suffix = { "" }

Boss_Encounter_Gate_Flame_Light = gate flame light
    .desc = { Boss_Encounter_Gate_Flame_Light_Desc }
    .suffix = { Boss_Encounter_Gate_Flame_Light_Suffix }

Boss_Encounter_Gate_Flame_Light_Desc = { "" }

Boss_Encounter_Gate_Flame_Light_Suffix = { "" }

Boss_Encounter_Gate_Cone_Light = gate light
    .desc = { Boss_Encounter_Gate_Cone_Light_Desc }
    .suffix = { Boss_Encounter_Gate_Cone_Light_Suffix }

Boss_Encounter_Gate_Cone_Light_Desc = { "" }

Boss_Encounter_Gate_Cone_Light_Suffix = { "" }

Boss_Encounter_Gate_Opening_Effect = gate opening effect
    .desc = { Boss_Encounter_Gate_Opening_Effect_Desc }
    .suffix = { Boss_Encounter_Gate_Opening_Effect_Suffix }

Boss_Encounter_Gate_Opening_Effect_Desc = { "" }

Boss_Encounter_Gate_Opening_Effect_Suffix = { "" }

Boss_Encounter_Return_Timer = { $minutes }:{ $seconds }

Boss_Encounter_Sentencing_Prefix = Preparing

Boss_Encounter_Sentencing_Occidens = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Occidens }

Boss_Encounter_Sentencing_Oriens = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Oriens }

Boss_Encounter_Sentencing_Septentrio = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Septentrio }

Boss_Encounter_Sentencing_Meridies = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Meridies }

Boss_Encounter_Sentencing_Cross = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Cross }

Boss_Encounter_Sentencing_Divide = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Divide }

Boss_Encounter_Sentencing_Severance = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Severance }

Boss_Encounter_Sentencing_Communion = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Communion }

Boss_Encounter_Sentencing_Deaths_Bliss = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Deaths_Bliss }

Boss_Encounter_Sentencing_Deaths_Blight = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Deaths_Blight }

Boss_Encounter_Sentencing_Constellation = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Constellation }

Boss_Encounter_Sentencing_Vigil = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Vigil }

Boss_Encounter_Moving_Cross = Moving Cross

Boss_Encounter_Crystal_Rain = Crystal rain

Boss_Encounter_Transitus_Septentrio = Transitus { Boss_Encounter_Septentrio }

Boss_Encounter_Edge_Cone_Septentrio = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Septentrio }

Boss_Encounter_Transitus_Oriens = Transitus { Boss_Encounter_Oriens }

Boss_Encounter_Edge_Cone_Oriens = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Oriens }

Boss_Encounter_Transitus_Meridies = Transitus { Boss_Encounter_Meridies }

Boss_Encounter_Edge_Cone_Meridies = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Meridies }

Boss_Encounter_Transitus_Occidens = Transitus { Boss_Encounter_Occidens }

Boss_Encounter_Edge_Cone_Occidens = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Occidens }

Boss_Encounter_Edge_Cone_Name = Edge assault

Boss_Encounter_Edge_Slash_Septentrio = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Septentrio }

Boss_Encounter_Edge_Slash_Oriens = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Oriens }

Boss_Encounter_Edge_Slash_Meridies = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Meridies }

Boss_Encounter_Edge_Slash_Occidens = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Occidens }

Boss_Encounter_Key = { Boss_Encounter_Name_Possessive } gate key
    .desc = { Boss_Encounter_Key_Desc }
    .suffix = { Boss_Encounter_Key_Suffix }

Boss_Encounter_Key_Desc = A key to the boss's arena gate.

Boss_Encounter_Key_Suffix = { "" }
