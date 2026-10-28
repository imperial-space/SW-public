Revenant_Boss_Name = Veyrath

Revenant_Boss_Summoning_Crystal = dark crystal
    .desc = { Revenant_Boss_Summoning_Crystal_Desc }
    .suffix = { Revenant_Boss_Summoning_Crystal_Suffix }

Revenant_Boss_Summoning_Crystal_Desc = Break it to summon { Revenant_Boss_Name }.

Revenant_Boss_Summoning_Crystal_Suffix = { "" }

Revenant_Boss_Execution = Execution

Revenant_Boss_Title = the Pale Harvester

Revenant_Boss_Name_Possessive = { Revenant_Boss_Name }'s

Revenant_Boss_Gate_Closed = The gate is sealed. Use its key to open it.

Revenant_Boss_Gate_Unavailable = You cannot enter the gate right now.

Revenant_Boss_Gate_Combat_Locked = The battle has begun. Wait for the arena to reset.

Revenant_Boss_Gate_Key_Required = Hold { Revenant_Boss_Key } and use it on the gate to open it.

Revenant_Boss_Gate_Arena_Failed = The arena could not be prepared. The gate remains sealed.

Revenant_Boss_Gate_Unlocked = The gate opens. The key is consumed.

Revenant_Boss_Victory_Loot_Time = Collect your loot and leave within { $minutes } minutes.

Revenant_Boss_Victory_Closing = The arena will close in one minute.

Revenant_Boss_Victory_Expired = Time is up. The arena has closed.

Revenant_Boss_Gate_Arrival_Blocked = The arrival point is blocked. The gate cannot transport you safely.

Revenant_Boss_Command_Bossgate_Desc = Links, opens, or closes a boss arena gate.

Revenant_Boss_Command_Bossgate_Help = Usage: bossgate link <gate ID or prototype> <arrival point ID or prototype>; bossgate open <gate>; bossgate close <gate>.

Revenant_Boss_Command_Bossgate_Select_Gate = Select exactly one arena gate by entity ID or prototype ID.

Revenant_Boss_Command_Bossgate_Select_Entrance = Select exactly one arena arrival point placed on a map.

Revenant_Boss_Command_Bossgate_Needs_Entrance = Link the gate to a valid arena arrival point before opening it.

Revenant_Boss_Command_Bossgate_Linked = Arena arrival point linked. Use { Revenant_Boss_Key } to open it, or bossgate open for an admin override.

Revenant_Boss_Command_Bossgate_Opened = Arena gate opened.

Revenant_Boss_Command_Bossgate_Closed = Arena gate closed.

Revenant_Boss_Rules_No_Building = You cannot create obstacles or change the terrain in this arena.

Revenant_Boss_Rules_No_Furniture = Leave blocking furniture outside the arena.

Revenant_Boss_Rules_Protected = This encounter cannot be moved or contained.

Revenant_Boss_Cast_Default_Name = Casting

Revenant_Boss_Cast_Preview_Name = Test cast

Revenant_Boss_Command_Bosscast_Desc = Previews or cancels a named boss cast without activating its encounter.

Revenant_Boss_Command_Bosscast_Help = Usage: bosscast <boss ID or prototype> [seconds] [ability name]; bosscast cancel <boss ID or prototype>. Defaults to a harmless 3-second test cast.

Revenant_Boss_Command_Bosscast_Select_Boss = Select exactly one boss by entity ID or prototype ID.

Revenant_Boss_Command_Bosscast_Duration = Preview duration must be between 0.1 and 120 seconds.

Revenant_Boss_Command_Bosscast_Name_Too_Long = Ability names must be at most 128 characters.

Revenant_Boss_Command_Bosscast_Unavailable = The boss must be alive, unfinished, unpaused, and not already casting.

Revenant_Boss_Command_Bosscast_Started = Harmless cast preview started.

Revenant_Boss_Command_Bosscast_Cancelled = Boss cast cancelled.

Revenant_Boss_Command_Bosscast_No_Cast = This boss is not casting.

Revenant_Boss_Command_Testboss_Desc = Starts a boss encounter with connected, living humanoid players.

Revenant_Boss_Command_Testboss_Help = Usage: testboss [boss prototype ID or entity ID]. Local encounters include players on the boss's grid; arena encounters require BossSpawnPosition markers.

Revenant_Boss_Command_Testboss_Select = Select exactly one spawned boss by prototype ID or entity ID, for example: testboss RevenantBoss.

Revenant_Boss_Command_Testboss_Start_Failed = Could not start the fight. The boss must be on a grid, inactive and unfinished, with living players available. Arena bosses also need BossSpawnPosition markers.

Revenant_Boss_Command_Testboss_Started = Started the encounter for { $boss }.

Revenant_Boss_Command_Crescentreap_Desc = Previews directional Reaps or a prepared Requiem combo without damage.

Revenant_Boss_Command_Crescentreap_Help = Usage: crescentreap <boss ID or prototype> [random|septentrio,oriens,...|cancel] [normal|slow|fast]; crescentreap <boss> <septentrio|oriens|meridies|occidens> [seconds]. Comma-separated combos require 2-4 directions. North/east/south/west and older directional aliases are also accepted. No arguments after the boss selects a random combo. All previews are harmless.

Revenant_Boss_Command_Midnightvigil_Desc = Previews Vigil without damage.

Revenant_Boss_Command_Midnightvigil_Help = Usage: midnightvigil <boss ID or prototype> [nw|ne|sw|se] [seconds]. The corner selects the safe brazier. Default: nw, 6 seconds. Harmless preview.

Revenant_Boss_Command_Blackout_Desc = Legacy alias for Vigil's harmless preview.

Revenant_Boss_Command_Blackout_Help = Usage: blackout <boss ID or prototype> [nw|ne|sw|se] [seconds]. Prefer midnightvigil. Default: nw, 6 seconds. Harmless preview.

Revenant_Boss_Command_Deathsembrace_Desc = Previews { Revenant_Boss_Deaths_Bliss } or { Revenant_Boss_Deaths_Blight } without damage.

Revenant_Boss_Command_Deathsembrace_Help = Usage: deathsembrace <boss ID or prototype> [rejection|invitation] [seconds]. Default: rejection, 6 seconds. Harmless preview.

Revenant_Boss_Command_Sepulchralcross_Desc = Previews Cross without damage.

Revenant_Boss_Command_Sepulchralcross_Help = Usage: sepulchralcross <boss ID or prototype> [seconds]. Default: 5 seconds. Harmless preview.

Revenant_Boss_Command_Sepulchraldivide_Desc = Previews Divide without damage.

Revenant_Boss_Command_Sepulchraldivide_Help = Usage: sepulchraldivide <boss ID or prototype> [seconds]. Default: 5 seconds. Harmless preview.

Revenant_Boss_Command_Reaperssentence_Desc = Previews Severance or Communion without damage.

Revenant_Boss_Command_Reaperssentence_Help = Usage: reaperssentence <boss ID or prototype> [severance|communion] [seconds]. Severance separates marked players; Communion gathers the party. Default: severance, 5 seconds. Requires living arena players. Harmless preview.

Revenant_Boss_Command_Paleconstellation_Desc = Previews Constellation without damage.

Revenant_Boss_Command_Paleconstellation_Help = Usage: paleconstellation <boss ID or prototype> [elegy|dirge] [seconds]. Elegy selects blue then purple; dirge selects purple then blue. Both casts display Constellation; players read the order from the colored flames. Default: elegy, 6 seconds. Harmless preview.

Revenant_Boss_Requiem = Requiem

Revenant_Boss_Sentencing = Sentencing { $attack }

Revenant_Boss_Sequence_Strike_Name = { $name }

Revenant_Boss_Command_Bosssequence_Desc = Previews { Revenant_Boss_Name_Possessive } prepared cleave combos without damage.

Revenant_Boss_Command_Bosssequence_Help = Usage: bosssequence <boss ID or prototype> [random|left,right,...|cancel] [normal|slow|fast]. Supply 2-4 directions: septentrio, oriens, meridies, occidens (or north, east, south, west; older aliases still work). Prefer crescentreap for mechanic-named commands. Normal: 2 seconds between hits; slow: 3; fast: 1.6. All previews are harmless. Start actual combat using testboss.

Revenant_Boss_Command_Bosssequence_Unavailable = Select { Revenant_Boss_Name } on the mapped arena, with the encounter unfinished, a configured sequence attack and no cast or sequence running.

Revenant_Boss_Command_Bosssequence_Started = Started a harmless prepared combo preview: { $count } strikes, { $interval } seconds apart. Preloads: { $preload } seconds; release: { $release } seconds.

Revenant_Boss_Command_Bosssequence_Cancelled = Cancelled the entire prepared combo and its warnings.

Revenant_Boss_Command_Bosssequence_No_Sequence = This boss has no prepared combo running.

Revenant_Boss_Command_Bosstelegraph_Desc = Previews harmless boss telegraphs and target markers.

Revenant_Boss_Command_Bosstelegraph_Help = Usage: bosstelegraph <boss ID or prototype> <septentrio|oriens|meridies|occidens|blackout-nw|blackout-ne|blackout-sw|blackout-se|rejection|invitation|cross|divide|severance|communion|elegy|dirge> [seconds]; bosstelegraph <boss> tank [player entity ID]; bosstelegraph <boss> clear. For Midnight Vigil use midnightvigil <boss> [nw|ne|sw|se] [seconds]. Older variant names remain aliases.

Revenant_Boss_Command_Bosstelegraph_Cleared = Cleared the cast preview and target marks.

Revenant_Boss_Command_Bosstelegraph_Tank_Failed = Select a living player on this boss's arena platform.

Revenant_Boss_Command_Bosstelegraph_Tank_Set = Set the main-tank mark. This does not start combat or deal damage.

Revenant_Boss_Command_Bosstelegraph_Duration = Preview duration must be between 1 and 120 seconds.

Revenant_Boss_Command_Bosstelegraph_Unavailable = Select { Revenant_Boss_Name } on the mapped arena with the encounter unfinished and no cast already running. Severance/Communion also require living arena players.

Revenant_Boss_Command_Bosstelegraph_Started = Started a harmless telegraph preview. No attack damage is applied.

Revenant_Boss_Occidens = Occidens

Revenant_Boss_Oriens = Oriens

Revenant_Boss_Septentrio = Septentrio

Revenant_Boss_Meridies = Meridies

Revenant_Boss_Cruciform_Vigil = Cruciform Vigil

Revenant_Boss_Sundered_Vigil = Sundered Vigil

Revenant_Boss_Dire_Requiem = Dire Requiem

Revenant_Boss_Vigil = Vigil

Revenant_Boss_Blackout_Nw = { Revenant_Boss_Vigil }

Revenant_Boss_Blackout_Ne = { Revenant_Boss_Vigil }

Revenant_Boss_Blackout_Sw = { Revenant_Boss_Vigil }

Revenant_Boss_Blackout_Se = { Revenant_Boss_Vigil }

Revenant_Boss_Deaths_Bliss = Death's Bliss

Revenant_Boss_Deaths_Blight = Death's Blight

Revenant_Boss_Cross = Cross

Revenant_Boss_Divide = Divide

Revenant_Boss_Severance = Severance

Revenant_Boss_Communion = Communion

Revenant_Boss_Constellation = Constellation

Revenant_Boss_Elegy = { Revenant_Boss_Constellation }

Revenant_Boss_Dirge = { Revenant_Boss_Constellation }

Revenant_Boss_Spread = { Revenant_Boss_Severance }

Revenant_Boss_Stack = { Revenant_Boss_Communion }

Revenant_Boss_Blue_First = { Revenant_Boss_Elegy }

Revenant_Boss_Purple_First = { Revenant_Boss_Dirge }

Revenant_Boss = { Revenant_Boss_Name }, { Revenant_Boss_Title }
    .desc = { Revenant_Boss_Desc }
    .suffix = { Revenant_Boss_Suffix }

Revenant_Boss_Desc = A hooded reaper armed with a scythe.

Revenant_Boss_Suffix = Arena encounter

Revenant_Boss_Gate = { Revenant_Boss_Name_Possessive } arena gate
    .desc = { Revenant_Boss_Gate_Desc }
    .suffix = { Revenant_Boss_Gate_Suffix }

Revenant_Boss_Gate_Desc = An ancient gate leading to { Revenant_Boss_Name_Possessive } arena.

Revenant_Boss_Gate_Suffix = Closed

Revenant_Boss_Arrival = { Revenant_Boss_Name_Possessive } arena arrival point
    .desc = { Revenant_Boss_Arrival_Desc }
    .suffix = { Revenant_Boss_Arrival_Suffix }

Revenant_Boss_Arrival_Desc = Marks a safe arrival point inside the arena.

Revenant_Boss_Arrival_Suffix = { "" }

Revenant_Boss_Key = { Revenant_Boss_Name_Possessive } gate key
    .desc = { Revenant_Boss_Key_Desc }
    .suffix = { Revenant_Boss_Key_Suffix }

Revenant_Boss_Key_Desc = A key to { Revenant_Boss_Name_Possessive } arena gate.

Revenant_Boss_Key_Suffix = { "" }

Revenant_Boss_Chest = { Revenant_Boss_Name_Possessive } coffer
    .desc = { Revenant_Boss_Chest_Desc }
    .suffix = { Revenant_Boss_Chest_Suffix }

Revenant_Boss_Chest_Desc = A heavy coffer left behind by { Revenant_Boss_Name }.

Revenant_Boss_Chest_Suffix = Boss reward

Revenant_Boss_Chasm = chasm
    .desc = { Revenant_Boss_Chasm_Desc }
    .suffix = { Revenant_Boss_Chasm_Suffix }

Revenant_Boss_Chasm_Desc = Falling is fatal. Your body returns to the arena entrance.

Revenant_Boss_Chasm_Suffix = { Revenant_Boss_Name_Possessive } arena

Revenant_Boss_Return_Flame = { Revenant_Boss_Name_Possessive } return flame
    .desc = { Revenant_Boss_Return_Flame_Desc }
    .suffix = { Revenant_Boss_Return_Flame_Suffix }

Revenant_Boss_Return_Flame_Desc = A lingering portal that returns victors to the entrance.

Revenant_Boss_Return_Flame_Suffix = { "" }

Revenant_Boss_Wall = arena wall
    .desc = { Revenant_Boss_Wall_Desc }
    .suffix = { Revenant_Boss_Wall_Suffix }

Revenant_Boss_Wall_Desc = An unyielding wall enclosing the arena.

Revenant_Boss_Wall_Suffix = { "" }

Revenant_Boss_Center = reserved arena center
    .desc = { Revenant_Boss_Center_Desc }
    .suffix = { Revenant_Boss_Center_Suffix }

Revenant_Boss_Center_Desc = Reserves the boss's position in the arena.

Revenant_Boss_Center_Suffix = { "" }

Revenant_Boss_Brazier = { Revenant_Boss_Name_Possessive } brazier
    .desc = { Revenant_Boss_Brazier_Desc }
    .suffix = { Revenant_Boss_Brazier_Suffix }

Revenant_Boss_Brazier_Desc = Stand beside the white flame when the arena goes dark.

Revenant_Boss_Brazier_Suffix = { "" }

Revenant_Boss_Telegraph = attack warning
    .desc = { Revenant_Boss_Telegraph_Desc }
    .suffix = { Revenant_Boss_Telegraph_Suffix }

Revenant_Boss_Telegraph_Desc = { "" }

Revenant_Boss_Telegraph_Suffix = { "" }

Revenant_Boss_Impact = attack impact
    .desc = { Revenant_Boss_Impact_Desc }
    .suffix = { Revenant_Boss_Impact_Suffix }

Revenant_Boss_Impact_Desc = { "" }

Revenant_Boss_Impact_Suffix = { "" }

Revenant_Boss_Tank_Buster_Marker = tank buster marker
    .desc = { Revenant_Boss_Tank_Buster_Marker_Desc }
    .suffix = { Revenant_Boss_Tank_Buster_Marker_Suffix }

Revenant_Boss_Tank_Buster_Marker_Desc = { "" }

Revenant_Boss_Tank_Buster_Marker_Suffix = { "" }

Revenant_Boss_Tank_Marker = tank marker
    .desc = { Revenant_Boss_Tank_Marker_Desc }
    .suffix = { Revenant_Boss_Tank_Marker_Suffix }

Revenant_Boss_Tank_Marker_Desc = { "" }

Revenant_Boss_Tank_Marker_Suffix = { "" }

Revenant_Boss_Spread_Marker = Severance marker
    .desc = { Revenant_Boss_Spread_Marker_Desc }
    .suffix = { Revenant_Boss_Spread_Marker_Suffix }

Revenant_Boss_Spread_Marker_Desc = { "" }

Revenant_Boss_Spread_Marker_Suffix = { "" }

Revenant_Boss_Stack_Marker = Communion marker
    .desc = { Revenant_Boss_Stack_Marker_Desc }
    .suffix = { Revenant_Boss_Stack_Marker_Suffix }

Revenant_Boss_Stack_Marker_Desc = { "" }

Revenant_Boss_Stack_Marker_Suffix = { "" }

Revenant_Boss_Constellation_Emitter = Constellation flame
    .desc = { Revenant_Boss_Constellation_Emitter_Desc }
    .suffix = { Revenant_Boss_Constellation_Emitter_Suffix }

Revenant_Boss_Constellation_Emitter_Desc = A flame marking the path of a Constellation beam.

Revenant_Boss_Constellation_Emitter_Suffix = { "" }

Revenant_Boss_Blue_Emitter = blue Constellation flame
    .desc = { Revenant_Boss_Blue_Emitter_Desc }
    .suffix = { Revenant_Boss_Blue_Emitter_Suffix }

Revenant_Boss_Blue_Emitter_Desc = { "" }

Revenant_Boss_Blue_Emitter_Suffix = { "" }

Revenant_Boss_Purple_Emitter = purple Constellation flame
    .desc = { Revenant_Boss_Purple_Emitter_Desc }
    .suffix = { Revenant_Boss_Purple_Emitter_Suffix }

Revenant_Boss_Purple_Emitter_Desc = { "" }

Revenant_Boss_Purple_Emitter_Suffix = { "" }

Revenant_Boss_Gate_Flame_Light = gate flame light
    .desc = { Revenant_Boss_Gate_Flame_Light_Desc }
    .suffix = { Revenant_Boss_Gate_Flame_Light_Suffix }

Revenant_Boss_Gate_Flame_Light_Desc = { "" }

Revenant_Boss_Gate_Flame_Light_Suffix = { "" }

Revenant_Boss_Gate_Cone_Light = gate light
    .desc = { Revenant_Boss_Gate_Cone_Light_Desc }
    .suffix = { Revenant_Boss_Gate_Cone_Light_Suffix }

Revenant_Boss_Gate_Cone_Light_Desc = { "" }

Revenant_Boss_Gate_Cone_Light_Suffix = { "" }

Revenant_Boss_Gate_Opening_Effect = gate opening effect
    .desc = { Revenant_Boss_Gate_Opening_Effect_Desc }
    .suffix = { Revenant_Boss_Gate_Opening_Effect_Suffix }

Revenant_Boss_Gate_Opening_Effect_Desc = { "" }

Revenant_Boss_Gate_Opening_Effect_Suffix = { "" }

Revenant_Boss_Arena = { Revenant_Boss_Name_Possessive } arena
    .desc = { Revenant_Boss_Arena_Desc }
    .suffix = { Revenant_Boss_Arena_Suffix }

Revenant_Boss_Arena_Desc = { "" }

Revenant_Boss_Arena_Suffix = { "" }

Revenant_Boss_Platform = { Revenant_Boss_Name_Possessive } combat platform
    .desc = { Revenant_Boss_Platform_Desc }
    .suffix = { Revenant_Boss_Platform_Suffix }

Revenant_Boss_Platform_Desc = { "" }

Revenant_Boss_Platform_Suffix = { "" }

Revenant_Boss_Scenery = { Revenant_Boss_Name_Possessive } arena surroundings
    .desc = { Revenant_Boss_Scenery_Desc }
    .suffix = { Revenant_Boss_Scenery_Suffix }

Revenant_Boss_Scenery_Desc = { "" }

Revenant_Boss_Scenery_Suffix = { "" }

Revenant_Boss_Floor = arena stone floor

Revenant_Boss_Pit_Floor = arena abyss

Revenant_Boss_Return_Timer = { $minutes }:{ $seconds }

Revenant_Boss_Sentencing_Prefix = Sentencing

Revenant_Boss_Sentencing_Occidens = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Occidens }

Revenant_Boss_Sentencing_Oriens = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Oriens }

Revenant_Boss_Sentencing_Septentrio = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Septentrio }

Revenant_Boss_Sentencing_Meridies = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Meridies }

Revenant_Boss_Sentencing_Cross = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Cross }

Revenant_Boss_Sentencing_Divide = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Divide }

Revenant_Boss_Sentencing_Severance = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Severance }

Revenant_Boss_Sentencing_Communion = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Communion }

Revenant_Boss_Sentencing_Deaths_Bliss = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Deaths_Bliss }

Revenant_Boss_Sentencing_Deaths_Blight = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Deaths_Blight }

Revenant_Boss_Sentencing_Constellation = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Constellation }

Revenant_Boss_Sentencing_Vigil = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Vigil }

ent-RevenantBoss = { Revenant_Boss }
    .desc = { Revenant_Boss_Desc }
    .suffix = { Revenant_Boss_Suffix }

Revenant_Boss_Moving_Cross = Crux Errans

Revenant_Boss_Crystal_Rain = Pluvia Funesta

Revenant_Boss_Transitus_Septentrio = Transitus { Revenant_Boss_Septentrio }

Revenant_Boss_Edge_Cone_Septentrio = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Septentrio }

Revenant_Boss_Transitus_Oriens = Transitus { Revenant_Boss_Oriens }

Revenant_Boss_Edge_Cone_Oriens = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Oriens }

Revenant_Boss_Transitus_Meridies = Transitus { Revenant_Boss_Meridies }

Revenant_Boss_Edge_Cone_Meridies = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Meridies }

Revenant_Boss_Transitus_Occidens = Transitus { Revenant_Boss_Occidens }

Revenant_Boss_Edge_Cone_Occidens = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Occidens }

Revenant_Boss_Edge_Cone_Name = Judicium Ultra Limen

Revenant_Boss_Edge_Slash_Septentrio = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Septentrio }

Revenant_Boss_Edge_Slash_Oriens = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Oriens }

Revenant_Boss_Edge_Slash_Meridies = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Meridies }

Revenant_Boss_Edge_Slash_Occidens = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Occidens }

Revenant_Boss_Scythe = Mercy’s End
    .desc = { Revenant_Boss_Scythe_Desc }
    .suffix = { "" }

Revenant_Boss_Scythe_Desc = A long scythe with a violet blade.
