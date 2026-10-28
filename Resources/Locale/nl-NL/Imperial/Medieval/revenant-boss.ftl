Revenant_Boss_Name = Veyrath

Revenant_Boss_Summoning_Crystal = donker kristal
    .desc = { Revenant_Boss_Summoning_Crystal_Desc }
    .suffix = { Revenant_Boss_Summoning_Crystal_Suffix }

Revenant_Boss_Summoning_Crystal_Desc = Breek het kristal om Veyrath op te roepen.

Revenant_Boss_Summoning_Crystal_Suffix = { "" }

Revenant_Boss_Execution = Executie

Revenant_Boss_Title = de Bleke Oogster

Revenant_Boss_Name_Possessive = { Revenant_Boss_Name }s

Revenant_Boss_Gate_Closed = De poort is verzegeld. Open hem met de sleutel.

Revenant_Boss_Gate_Unavailable = Je kunt de poort nu niet betreden.

Revenant_Boss_Gate_Combat_Locked = Het gevecht is begonnen. Wacht tot de arena opnieuw klaar is.

Revenant_Boss_Gate_Key_Required = Houd de { Revenant_Boss_Key } vast en gebruik hem op de poort.

Revenant_Boss_Gate_Arena_Failed = De arena kon niet worden voorbereid. De poort blijft gesloten.

Revenant_Boss_Gate_Unlocked = De poort opent. De sleutel is verbruikt.

Revenant_Boss_Victory_Loot_Time = Verzamel je buit en vertrek binnen { $minutes } minuten.

Revenant_Boss_Victory_Closing = De arena sluit over één minuut.

Revenant_Boss_Victory_Expired = De tijd is om. De arena is gesloten.

Revenant_Boss_Gate_Arrival_Blocked = Het aankomstpunt is geblokkeerd. Veilig teleporteren is niet mogelijk.

Revenant_Boss_Command_Bossgate_Desc = Koppelt, opent of sluit een arenapoort.

Revenant_Boss_Command_Bossgate_Help = Gebruik: bossgate link <poort-ID of prototype> <aankomstpunt-ID of prototype>; bossgate open <gate>; bossgate close <gate>.. Koppelt het aankomstpunt en opent of sluit de poort.

Revenant_Boss_Command_Bossgate_Select_Gate = Selecteer één arenapoort via entiteits-ID of prototype-ID.

Revenant_Boss_Command_Bossgate_Select_Entrance = Selecteer één aankomstpunt op een kaart.

Revenant_Boss_Command_Bossgate_Needs_Entrance = Koppel de poort aan een geldig aankomstpunt voordat je hem opent.

Revenant_Boss_Command_Bossgate_Linked = Aankomstpunt gekoppeld. Open met { Revenant_Boss_Key } of de beheerdersopdracht bossgate open.

Revenant_Boss_Command_Bossgate_Opened = Arenapoort geopend.

Revenant_Boss_Command_Bossgate_Closed = Arenapoort gesloten.

Revenant_Boss_Rules_No_Building = Je kunt in deze arena geen obstakels maken of de grond wijzigen.

Revenant_Boss_Rules_No_Furniture = Laat blokkerend meubilair buiten de arena.

Revenant_Boss_Rules_Protected = Dit gevechtsobject kan niet worden verplaatst of opgeborgen.

Revenant_Boss_Cast_Default_Name = Voorbereiden

Revenant_Boss_Cast_Preview_Name = Testaanval

Revenant_Boss_Command_Bosscast_Desc = Toont of annuleert een benoemde baasaanval zonder het gevecht te starten.

Revenant_Boss_Command_Bosscast_Help = Gebruik: bosscast <baas-ID of prototype> [seconden] [vaardigheidsnaam]; bosscast cancel <baas-ID of prototype>. Standaard: een onschadelijke proefaanval van 3 seconden.

Revenant_Boss_Command_Bosscast_Select_Boss = Selecteer één baas via entiteits-ID of prototype-ID.

Revenant_Boss_Command_Bosscast_Duration = De voorbeeldduur moet tussen 0,1 en 120 seconden liggen.

Revenant_Boss_Command_Bosscast_Name_Too_Long = De vaardigheidsnaam mag maximaal 128 tekens bevatten.

Revenant_Boss_Command_Bosscast_Unavailable = De baas moet leven, onverslagen en niet gepauzeerd zijn en geen andere aanval voorbereiden.

Revenant_Boss_Command_Bosscast_Started = Onschadelijk aanvalvoorbeeld gestart.

Revenant_Boss_Command_Bosscast_Cancelled = Baasaanval geannuleerd.

Revenant_Boss_Command_Bosscast_No_Cast = Deze baas bereidt momenteel geen aanval voor.

Revenant_Boss_Command_Testboss_Desc = Start een baasgevecht met verbonden, levende humanoïde spelers.

Revenant_Boss_Command_Testboss_Help = Gebruik: testboss [baasprototype-ID of entiteits-ID]. Lokale gevechten gebruiken spelers op het raster van de baas; arenagevechten vereisen BossSpawnPosition-markeringen.

Revenant_Boss_Command_Testboss_Select = Selecteer één bestaande baas via prototype-ID of entiteits-ID, bijvoorbeeld: testboss RevenantBoss.

Revenant_Boss_Command_Testboss_Start_Failed = Het gevecht kon niet starten. De baas moet op een raster staan, inactief en onverslagen zijn; levende spelers zijn vereist. Arenabazen hebben ook BossSpawnPosition-markeringen nodig.

Revenant_Boss_Command_Testboss_Started = Gevecht met { $boss } gestart.

Revenant_Boss_Command_Crescentreap_Desc = Toont gerichte slagen of een voorbereide Requiem-combinatie zonder schade.

Revenant_Boss_Command_Crescentreap_Help = Gebruik: crescentreap <baas-ID of prototype> [random|septentrio,oriens,...|cancel] [normal|slow|fast]; crescentreap <boss> <septentrio|oriens|meridies|occidens> [seconden]. Combinaties bevatten 2-4 richtingen, gescheiden door komma’s. North/east/south/west en oude aliassen werken ook. Zonder extra argumenten wordt een willekeurige combinatie gekozen. Geen schade.

Revenant_Boss_Command_Midnightvigil_Desc = Toont Vigil zonder schade.

Revenant_Boss_Command_Midnightvigil_Help = Gebruik: midnightvigil <baas-ID of prototype> [nw|ne|sw|se] [seconden]. De hoek bepaalt de veilige vuurkorf. Standaard: nw, 6 seconden. Geen schade.

Revenant_Boss_Command_Blackout_Desc = Oude alias voor het onschadelijke Vigil-voorbeeld.

Revenant_Boss_Command_Blackout_Help = Gebruik: blackout <baas-ID of prototype> [nw|ne|sw|se] [seconden]. Gebruik bij voorkeur midnightvigil. Standaard: nw, 6 seconden. Geen schade.

Revenant_Boss_Command_Deathsembrace_Desc = Toont { Revenant_Boss_Deaths_Bliss } of { Revenant_Boss_Deaths_Blight } zonder schade.

Revenant_Boss_Command_Deathsembrace_Help = Gebruik: deathsembrace <baas-ID of prototype> [rejection|invitation] [seconden]. Standaard: rejection, 6 seconden. Geen schade.

Revenant_Boss_Command_Sepulchralcross_Desc = Toont Kruis zonder schade.

Revenant_Boss_Command_Sepulchralcross_Help = Gebruik: sepulchralcross <baas-ID of prototype> [seconden]. Standaard: 5 seconden. Geen schade.

Revenant_Boss_Command_Sepulchraldivide_Desc = Toont Scheiding zonder schade.

Revenant_Boss_Command_Sepulchraldivide_Help = Gebruik: sepulchraldivide <baas-ID of prototype> [seconden]. Standaard: 5 seconden. Geen schade.

Revenant_Boss_Command_Reaperssentence_Desc = Toont Scheiding of Communie zonder schade.

Revenant_Boss_Command_Reaperssentence_Help = Gebruik: reaperssentence <baas-ID of prototype> [severance|communion] [seconden]. Scheiding verspreidt gemarkeerde spelers; Communie verzamelt de groep. Standaard: severance, 5 seconden. Levende arenaspelers vereist. Geen schade.

Revenant_Boss_Command_Paleconstellation_Desc = Toont Sterrenbeeld zonder schade.

Revenant_Boss_Command_Paleconstellation_Help = Gebruik: paleconstellation <baas-ID of prototype> [elegy|dirge] [seconden]. elegy: blauw, dan paars; dirge: andersom. Lees de volgorde aan de vlamkleuren. Standaard: elegy, 6 seconden. Geen schade.

Revenant_Boss_Requiem = Requiem

Revenant_Boss_Sentencing = Vonnis: { $attack }

Revenant_Boss_Sequence_Strike_Name = { $name }

Revenant_Boss_Command_Bosssequence_Desc = Toont voorbereide slagcombinaties van Veyrath zonder schade.

Revenant_Boss_Command_Bosssequence_Help = Gebruik: bosssequence <baas-ID of prototype> [random|left,right,...|cancel] [normal|slow|fast]. Geef 2-4 richtingen: septentrio, oriens, meridies, occidens; north/east/south/west en oude aliassen werken ook. Gebruik bij voorkeur crescentreap. Interval: normal 2 seconden, slow 3, fast 1,6. Geen schade; start een echt gevecht met testboss.

Revenant_Boss_Command_Bosssequence_Unavailable = Selecteer { Revenant_Boss_Name } in de arena. Het gevecht moet onafgerond zijn, met een ingestelde aanvalsreeks en zonder actieve aanval of combinatie.

Revenant_Boss_Command_Bosssequence_Started = Onschadelijk combinatievoorbeeld gestart: { $count } slagen, telkens { $interval } seconden ertussen. Voorbereiding: { $preload } seconden; ontlading: { $release } seconden.

Revenant_Boss_Command_Bosssequence_Cancelled = De volledige voorbereide combinatie en waarschuwingen zijn geannuleerd.

Revenant_Boss_Command_Bosssequence_No_Sequence = Deze baas heeft geen actieve voorbereide combinatie.

Revenant_Boss_Command_Bosstelegraph_Desc = Toont onschadelijke aanvalswaarschuwingen en doelmarkeringen.

Revenant_Boss_Command_Bosstelegraph_Help = Gebruik: bosstelegraph <baas-ID of prototype> <septentrio|oriens|meridies|occidens|blackout-nw|blackout-ne|blackout-sw|blackout-se|rejection|invitation|cross|divide|severance|communion|elegy|dirge> [seconden]; bosstelegraph <boss> tank [spelerentiteits-ID]; bosstelegraph <boss> clear. Gebruik voor Vigil midnightvigil <boss> [nw|ne|sw|se] [seconden]. Oude variantnamen blijven aliassen. Geen schade.

Revenant_Boss_Command_Bosstelegraph_Cleared = Aanvalvoorbeeld en doelmarkeringen verwijderd.

Revenant_Boss_Command_Bosstelegraph_Tank_Failed = Selecteer een levende speler op het arenaplatform van deze baas.

Revenant_Boss_Command_Bosstelegraph_Tank_Set = Hoofdtankmarkering ingesteld. Dit start geen gevecht en veroorzaakt geen schade.

Revenant_Boss_Command_Bosstelegraph_Duration = De voorbeeldduur moet tussen 1 en 120 seconden liggen.

Revenant_Boss_Command_Bosstelegraph_Unavailable = Selecteer { Revenant_Boss_Name } in de arena, met het gevecht onafgerond en zonder actieve aanval. Scheiding en Communie vereisen levende arenaspelers.

Revenant_Boss_Command_Bosstelegraph_Started = Onschadelijk waarschuwingsvoorbeeld gestart. Er wordt geen aanvalsschade toegebracht.

Revenant_Boss_Occidens = Occidens

Revenant_Boss_Oriens = Oriens

Revenant_Boss_Septentrio = Septentrio

Revenant_Boss_Meridies = Meridies

Revenant_Boss_Cruciform_Vigil = Kruiswake

Revenant_Boss_Sundered_Vigil = Verdeelde wake

Revenant_Boss_Dire_Requiem = Dreigend requiem

Revenant_Boss_Vigil = Wake

Revenant_Boss_Blackout_Nw = { Revenant_Boss_Vigil }

Revenant_Boss_Blackout_Ne = { Revenant_Boss_Vigil }

Revenant_Boss_Blackout_Sw = { Revenant_Boss_Vigil }

Revenant_Boss_Blackout_Se = { Revenant_Boss_Vigil }

Revenant_Boss_Deaths_Bliss = Doodszaligheid

Revenant_Boss_Deaths_Blight = Doodsverderf

Revenant_Boss_Cross = Kruis

Revenant_Boss_Divide = Verdeling

Revenant_Boss_Severance = Scheiding

Revenant_Boss_Communion = Gemeenschap

Revenant_Boss_Constellation = Sterrenbeeld

Revenant_Boss_Elegy = { Revenant_Boss_Constellation }

Revenant_Boss_Dirge = { Revenant_Boss_Constellation }

Revenant_Boss_Spread = { Revenant_Boss_Severance }

Revenant_Boss_Stack = { Revenant_Boss_Communion }

Revenant_Boss_Blue_First = { Revenant_Boss_Elegy }

Revenant_Boss_Purple_First = { Revenant_Boss_Dirge }

Revenant_Boss = { Revenant_Boss_Name }, { Revenant_Boss_Title }
    .desc = { Revenant_Boss_Desc }
    .suffix = { Revenant_Boss_Suffix }

Revenant_Boss_Desc = Een oogster met een kap, gewapend met een zeis.

Revenant_Boss_Suffix = Arenagevecht

Revenant_Boss_Gate = poort naar { Revenant_Boss_Name_Possessive } arena
    .desc = { Revenant_Boss_Gate_Desc }
    .suffix = { Revenant_Boss_Gate_Suffix }

Revenant_Boss_Gate_Desc = Een oude poort naar de arena van Veyrath.

Revenant_Boss_Gate_Suffix = Gesloten

Revenant_Boss_Arrival = aankomstpunt van { Revenant_Boss_Name_Possessive } arena
    .desc = { Revenant_Boss_Arrival_Desc }
    .suffix = { Revenant_Boss_Arrival_Suffix }

Revenant_Boss_Arrival_Desc = Markeert een veilige aankomstplek in de arena.

Revenant_Boss_Arrival_Suffix = { "" }

Revenant_Boss_Key = sleutel van { Revenant_Boss_Name_Possessive } poort
    .desc = { Revenant_Boss_Key_Desc }
    .suffix = { Revenant_Boss_Key_Suffix }

Revenant_Boss_Key_Desc = Een sleutel voor de poort naar Veyraths arena.

Revenant_Boss_Key_Suffix = { "" }

Revenant_Boss_Chest = { Revenant_Boss_Name_Possessive } kist
    .desc = { Revenant_Boss_Chest_Desc }
    .suffix = { Revenant_Boss_Chest_Suffix }

Revenant_Boss_Chest_Desc = Een zware kist die Veyrath heeft achtergelaten.

Revenant_Boss_Chest_Suffix = Baasbeloning

Revenant_Boss_Chasm = afgrond
    .desc = { Revenant_Boss_Chasm_Desc }
    .suffix = { Revenant_Boss_Chasm_Suffix }

Revenant_Boss_Chasm_Desc = Een val is dodelijk. Je lichaam keert terug naar de ingang van de arena.

Revenant_Boss_Chasm_Suffix = Veyraths arena

Revenant_Boss_Return_Flame = { Revenant_Boss_Name_Possessive } terugkeervlam
    .desc = { Revenant_Boss_Return_Flame_Desc }
    .suffix = { Revenant_Boss_Return_Flame_Suffix }

Revenant_Boss_Return_Flame_Desc = Een achtergebleven portaal brengt de overwinnaars terug naar de ingang.

Revenant_Boss_Return_Flame_Suffix = { "" }

Revenant_Boss_Wall = arenamuur
    .desc = { Revenant_Boss_Wall_Desc }
    .suffix = { Revenant_Boss_Wall_Suffix }

Revenant_Boss_Wall_Desc = Een onwrikbare muur rondom de arena.

Revenant_Boss_Wall_Suffix = { "" }

Revenant_Boss_Center = gereserveerd arenamidden
    .desc = { Revenant_Boss_Center_Desc }
    .suffix = { Revenant_Boss_Center_Suffix }

Revenant_Boss_Center_Desc = Reserveert de plek van de baas in de arena.

Revenant_Boss_Center_Suffix = { "" }

Revenant_Boss_Brazier = { Revenant_Boss_Name_Possessive } vuurkorf
    .desc = { Revenant_Boss_Brazier_Desc }
    .suffix = { Revenant_Boss_Brazier_Suffix }

Revenant_Boss_Brazier_Desc = Blijf bij de witte vlam wanneer de arena donker wordt.

Revenant_Boss_Brazier_Suffix = { "" }

Revenant_Boss_Telegraph = aanvalswaarschuwing
    .desc = { Revenant_Boss_Telegraph_Desc }
    .suffix = { Revenant_Boss_Telegraph_Suffix }

Revenant_Boss_Telegraph_Desc = { "" }

Revenant_Boss_Telegraph_Suffix = { "" }

Revenant_Boss_Impact = inslag
    .desc = { Revenant_Boss_Impact_Desc }
    .suffix = { Revenant_Boss_Impact_Suffix }

Revenant_Boss_Impact_Desc = { "" }

Revenant_Boss_Impact_Suffix = { "" }

Revenant_Boss_Tank_Buster_Marker = tankbrekermarkering
    .desc = { Revenant_Boss_Tank_Buster_Marker_Desc }
    .suffix = { Revenant_Boss_Tank_Buster_Marker_Suffix }

Revenant_Boss_Tank_Buster_Marker_Desc = { "" }

Revenant_Boss_Tank_Buster_Marker_Suffix = { "" }

Revenant_Boss_Tank_Marker = tankmarkering
    .desc = { Revenant_Boss_Tank_Marker_Desc }
    .suffix = { Revenant_Boss_Tank_Marker_Suffix }

Revenant_Boss_Tank_Marker_Desc = { "" }

Revenant_Boss_Tank_Marker_Suffix = { "" }

Revenant_Boss_Spread_Marker = Scheidingsmarkering
    .desc = { Revenant_Boss_Spread_Marker_Desc }
    .suffix = { Revenant_Boss_Spread_Marker_Suffix }

Revenant_Boss_Spread_Marker_Desc = { "" }

Revenant_Boss_Spread_Marker_Suffix = { "" }

Revenant_Boss_Stack_Marker = Gemeenschapsmarkering
    .desc = { Revenant_Boss_Stack_Marker_Desc }
    .suffix = { Revenant_Boss_Stack_Marker_Suffix }

Revenant_Boss_Stack_Marker_Desc = { "" }

Revenant_Boss_Stack_Marker_Suffix = { "" }

Revenant_Boss_Constellation_Emitter = Sterrenbeeldvlam
    .desc = { Revenant_Boss_Constellation_Emitter_Desc }
    .suffix = { Revenant_Boss_Constellation_Emitter_Suffix }

Revenant_Boss_Constellation_Emitter_Desc = Een vlam die het pad van een Sterrenbeeldstraal markeert.

Revenant_Boss_Constellation_Emitter_Suffix = { "" }

Revenant_Boss_Blue_Emitter = blauwe Sterrenbeeldvlam
    .desc = { Revenant_Boss_Blue_Emitter_Desc }
    .suffix = { Revenant_Boss_Blue_Emitter_Suffix }

Revenant_Boss_Blue_Emitter_Desc = { "" }

Revenant_Boss_Blue_Emitter_Suffix = { "" }

Revenant_Boss_Purple_Emitter = paarse Sterrenbeeldvlam
    .desc = { Revenant_Boss_Purple_Emitter_Desc }
    .suffix = { Revenant_Boss_Purple_Emitter_Suffix }

Revenant_Boss_Purple_Emitter_Desc = { "" }

Revenant_Boss_Purple_Emitter_Suffix = { "" }

Revenant_Boss_Gate_Flame_Light = poortvlamlicht
    .desc = { Revenant_Boss_Gate_Flame_Light_Desc }
    .suffix = { Revenant_Boss_Gate_Flame_Light_Suffix }

Revenant_Boss_Gate_Flame_Light_Desc = { "" }

Revenant_Boss_Gate_Flame_Light_Suffix = { "" }

Revenant_Boss_Gate_Cone_Light = poortlicht
    .desc = { Revenant_Boss_Gate_Cone_Light_Desc }
    .suffix = { Revenant_Boss_Gate_Cone_Light_Suffix }

Revenant_Boss_Gate_Cone_Light_Desc = { "" }

Revenant_Boss_Gate_Cone_Light_Suffix = { "" }

Revenant_Boss_Gate_Opening_Effect = poortopening
    .desc = { Revenant_Boss_Gate_Opening_Effect_Desc }
    .suffix = { Revenant_Boss_Gate_Opening_Effect_Suffix }

Revenant_Boss_Gate_Opening_Effect_Desc = { "" }

Revenant_Boss_Gate_Opening_Effect_Suffix = { "" }

Revenant_Boss_Arena = { Revenant_Boss_Name_Possessive } arena
    .desc = { Revenant_Boss_Arena_Desc }
    .suffix = { Revenant_Boss_Arena_Suffix }

Revenant_Boss_Arena_Desc = { "" }

Revenant_Boss_Arena_Suffix = { "" }

Revenant_Boss_Platform = { Revenant_Boss_Name_Possessive } gevechtsplatform
    .desc = { Revenant_Boss_Platform_Desc }
    .suffix = { Revenant_Boss_Platform_Suffix }

Revenant_Boss_Platform_Desc = { "" }

Revenant_Boss_Platform_Suffix = { "" }

Revenant_Boss_Scenery = omgeving van Veyraths arena
    .desc = { Revenant_Boss_Scenery_Desc }
    .suffix = { Revenant_Boss_Scenery_Suffix }

Revenant_Boss_Scenery_Desc = { "" }

Revenant_Boss_Scenery_Suffix = { "" }

Revenant_Boss_Floor = stenen arenavloer

Revenant_Boss_Pit_Floor = arena-afgrond

Revenant_Boss_Return_Timer = { $minutes }:{ $seconds }

Revenant_Boss_Sentencing_Prefix = Vonnis:

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

ent-RevenantBossCoffer = { Revenant_Boss_Chest }
    .desc = { Revenant_Boss_Chest_Desc }
    .suffix = { Revenant_Boss_Chest_Suffix }

ent-RevenantBossGate = { Revenant_Boss_Gate }
    .desc = { Revenant_Boss_Gate_Desc }
    .suffix = { Revenant_Boss_Gate_Suffix }

ent-RevenantBossArenaEntrance = { Revenant_Boss_Arrival }
    .desc = { Revenant_Boss_Arrival_Desc }
    .suffix = { Revenant_Boss_Arrival_Suffix }

ent-RevenantBossGateKey = { Revenant_Boss_Key }
    .desc = { Revenant_Boss_Key_Desc }
    .suffix = { Revenant_Boss_Key_Suffix }

ent-RevenantBossArenaChasm = { Revenant_Boss_Chasm }
    .desc = { Revenant_Boss_Chasm_Desc }
    .suffix = { Revenant_Boss_Chasm_Suffix }

ent-RevenantBossVictoryExit = { Revenant_Boss_Return_Flame }
    .desc = { Revenant_Boss_Return_Flame_Desc }
    .suffix = { Revenant_Boss_Return_Flame_Suffix }

boss-arena-gate-closed = { Revenant_Boss_Gate_Closed }

boss-arena-gate-unavailable = { Revenant_Boss_Gate_Unavailable }

boss-arena-gate-combat-locked = { Revenant_Boss_Gate_Combat_Locked }

boss-arena-gate-key-required = { Revenant_Boss_Gate_Key_Required }

boss-arena-gate-arena-failed = { Revenant_Boss_Gate_Arena_Failed }

boss-arena-gate-unlocked = { Revenant_Boss_Gate_Unlocked }

boss-arena-victory-loot-time = { Revenant_Boss_Victory_Loot_Time }

boss-arena-victory-closing = { Revenant_Boss_Victory_Closing }

boss-arena-victory-expired = { Revenant_Boss_Victory_Expired }

boss-arena-gate-arrival-blocked = { Revenant_Boss_Gate_Arrival_Blocked }

cmd-bossgate-desc = { Revenant_Boss_Command_Bossgate_Desc }

cmd-bossgate-help = { Revenant_Boss_Command_Bossgate_Help }

cmd-bossgate-select-gate = { Revenant_Boss_Command_Bossgate_Select_Gate }

cmd-bossgate-select-entrance = { Revenant_Boss_Command_Bossgate_Select_Entrance }

cmd-bossgate-needs-entrance = { Revenant_Boss_Command_Bossgate_Needs_Entrance }

cmd-bossgate-linked = { Revenant_Boss_Command_Bossgate_Linked }

cmd-bossgate-opened = { Revenant_Boss_Command_Bossgate_Opened }

cmd-bossgate-closed = { Revenant_Boss_Command_Bossgate_Closed }

boss-arena-rules-no-building = { Revenant_Boss_Rules_No_Building }

boss-arena-rules-no-furniture = { Revenant_Boss_Rules_No_Furniture }

boss-arena-rules-protected = { Revenant_Boss_Rules_Protected }

boss-cast-default-name = { Revenant_Boss_Cast_Default_Name }

boss-cast-preview-name = { Revenant_Boss_Cast_Preview_Name }

cmd-bosscast-desc = { Revenant_Boss_Command_Bosscast_Desc }

cmd-bosscast-help = { Revenant_Boss_Command_Bosscast_Help }

cmd-bosscast-select-boss = { Revenant_Boss_Command_Bosscast_Select_Boss }

cmd-bosscast-duration = { Revenant_Boss_Command_Bosscast_Duration }

cmd-bosscast-name-too-long = { Revenant_Boss_Command_Bosscast_Name_Too_Long }

cmd-bosscast-unavailable = { Revenant_Boss_Command_Bosscast_Unavailable }

cmd-bosscast-started = { Revenant_Boss_Command_Bosscast_Started }

cmd-bosscast-cancelled = { Revenant_Boss_Command_Bosscast_Cancelled }

cmd-bosscast-no-cast = { Revenant_Boss_Command_Bosscast_No_Cast }

cmd-testboss-desc = { Revenant_Boss_Command_Testboss_Desc }

cmd-testboss-help = { Revenant_Boss_Command_Testboss_Help }

cmd-testboss-select = { Revenant_Boss_Command_Testboss_Select }

cmd-testboss-start-failed = { Revenant_Boss_Command_Testboss_Start_Failed }

cmd-testboss-started = { Revenant_Boss_Command_Testboss_Started }

cmd-crescentreap-desc = { Revenant_Boss_Command_Crescentreap_Desc }

cmd-crescentreap-help = { Revenant_Boss_Command_Crescentreap_Help }

cmd-midnightvigil-desc = { Revenant_Boss_Command_Midnightvigil_Desc }

cmd-midnightvigil-help = { Revenant_Boss_Command_Midnightvigil_Help }

cmd-blackout-desc = { Revenant_Boss_Command_Blackout_Desc }

cmd-blackout-help = { Revenant_Boss_Command_Blackout_Help }

cmd-deathsembrace-desc = { Revenant_Boss_Command_Deathsembrace_Desc }

cmd-deathsembrace-help = { Revenant_Boss_Command_Deathsembrace_Help }

cmd-sepulchralcross-desc = { Revenant_Boss_Command_Sepulchralcross_Desc }

cmd-sepulchralcross-help = { Revenant_Boss_Command_Sepulchralcross_Help }

cmd-sepulchraldivide-desc = { Revenant_Boss_Command_Sepulchraldivide_Desc }

cmd-sepulchraldivide-help = { Revenant_Boss_Command_Sepulchraldivide_Help }

cmd-reaperssentence-desc = { Revenant_Boss_Command_Reaperssentence_Desc }

cmd-reaperssentence-help = { Revenant_Boss_Command_Reaperssentence_Help }

cmd-paleconstellation-desc = { Revenant_Boss_Command_Paleconstellation_Desc }

cmd-paleconstellation-help = { Revenant_Boss_Command_Paleconstellation_Help }

boss-sequence-release-name = { Revenant_Boss_Requiem }

boss-sequence-preload-name = { Revenant_Boss_Sentencing }

boss-sequence-strike-name = { Revenant_Boss_Sequence_Strike_Name }

cmd-bosssequence-desc = { Revenant_Boss_Command_Bosssequence_Desc }

cmd-bosssequence-help = { Revenant_Boss_Command_Bosssequence_Help }

cmd-bosssequence-unavailable = { Revenant_Boss_Command_Bosssequence_Unavailable }

cmd-bosssequence-started = { Revenant_Boss_Command_Bosssequence_Started }

cmd-bosssequence-cancelled = { Revenant_Boss_Command_Bosssequence_Cancelled }

cmd-bosssequence-no-sequence = { Revenant_Boss_Command_Bosssequence_No_Sequence }

cmd-bosstelegraph-desc = { Revenant_Boss_Command_Bosstelegraph_Desc }

cmd-bosstelegraph-help = { Revenant_Boss_Command_Bosstelegraph_Help }

cmd-bosstelegraph-cleared = { Revenant_Boss_Command_Bosstelegraph_Cleared }

cmd-bosstelegraph-tank-failed = { Revenant_Boss_Command_Bosstelegraph_Tank_Failed }

cmd-bosstelegraph-tank-set = { Revenant_Boss_Command_Bosstelegraph_Tank_Set }

cmd-bosstelegraph-duration = { Revenant_Boss_Command_Bosstelegraph_Duration }

cmd-bosstelegraph-unavailable = { Revenant_Boss_Command_Bosstelegraph_Unavailable }

cmd-bosstelegraph-started = { Revenant_Boss_Command_Bosstelegraph_Started }

boss-telegraph-name-sinistra = { Revenant_Boss_Occidens }

boss-telegraph-name-dextra = { Revenant_Boss_Oriens }

boss-telegraph-name-frons = { Revenant_Boss_Septentrio }

boss-telegraph-name-tergum = { Revenant_Boss_Meridies }

boss-telegraph-name-cruciform-vigil = { Revenant_Boss_Cruciform_Vigil }

boss-telegraph-name-sundered-vigil = { Revenant_Boss_Sundered_Vigil }

boss-telegraph-name-dire-requiem = { Revenant_Boss_Dire_Requiem }

boss-telegraph-name-midnight-vigil = { Revenant_Boss_Vigil }

boss-telegraph-name-blackout-nw = { Revenant_Boss_Blackout_Nw }

boss-telegraph-name-blackout-ne = { Revenant_Boss_Blackout_Ne }

boss-telegraph-name-blackout-sw = { Revenant_Boss_Blackout_Sw }

boss-telegraph-name-blackout-se = { Revenant_Boss_Blackout_Se }

boss-telegraph-name-rejection = { Revenant_Boss_Deaths_Bliss }

boss-telegraph-name-invitation = { Revenant_Boss_Deaths_Blight }

boss-telegraph-name-cross = { Revenant_Boss_Cross }

boss-telegraph-name-divide = { Revenant_Boss_Divide }

boss-telegraph-name-severance = { Revenant_Boss_Severance }

boss-telegraph-name-communion = { Revenant_Boss_Communion }

boss-telegraph-name-constellation = { Revenant_Boss_Constellation }

boss-telegraph-name-elegy = { Revenant_Boss_Elegy }

boss-telegraph-name-dirge = { Revenant_Boss_Dirge }

boss-telegraph-name-spread = { Revenant_Boss_Spread }

boss-telegraph-name-stack = { Revenant_Boss_Stack }

boss-telegraph-name-blue-first = { Revenant_Boss_Blue_First }

boss-telegraph-name-purple-first = { Revenant_Boss_Purple_First }

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

Revenant_Boss_Scythe_Desc = Een lange zeis met een violet lemmet.
