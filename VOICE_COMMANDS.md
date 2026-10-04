# AdvancedK9 voice commands — Build 878

This reference lists all 57 commands registered in this build and every built-in English alias. Available commands can still require a target, equipment, certification, health eligibility or a compatible integration; this is not a list of features certified by testing.

## How to speak commands

Go on duty, enable/configure voice transcription, hold the push-to-talk key (default **V**), speak and release. Use the configured dog name (for example **Rex**) or **K9**, **K 9**, **K nine**, **kay nine**, or **canine** in each dog command. Example: **Rex, lie down**. An alias containing K9 already supplies the wake word.

**Deploy K9** works before Rex is deployed. Stand within 7m of a configured station kennel. To return him, say **Rex, kennel up** near the kennel. Bare **kennel up** does not include a wake word. Deploy/dismiss share a state-dependent action.

Say **Dispatch, call EMS**, **Dispatch, call transport**, **Dispatch, request perimeter**, or **Dispatch, call bomb squad** for service requests. These dispatch-prefixed phrases do not require a dog-name wake word. The current undeployed voice filter admits only Deploy/Dismiss, so deploy the K9 before using voice service requests.

Leash, camera and carry/set-down phrases share toggle actions: check the current state before repeating an opposite phrase. A newer accepted command replaces an active dog task.

## All English phrases

Add **Rex,** (or your dog's name / K9) before any phrase below unless it already contains a wake word. For the four dispatch services, **Dispatch,** is also accepted.

| Action / configuration key | Accepted phrases |
| --- | --- |
| Deploy / Dismiss (`SpawnDismiss`) | deploy k9; deploy the dog; bring out the dog; partner up; send out the dog; dismiss k9; kennel up; end shift; dismiss |
| Follow (`Follow`) | follow me; stay with me; move with me; on me; with me; follow |
| Heel (`Heel`) | come to heel; heel up; get to heel; by my side; at heel; heel; heal |
| Sit (`Sit`) | sit down; take a seat; park it; sit |
| Down (`LieDown`) | lie down; lay down; get low; down on the ground; go down; down |
| Stay (`Stay`) | stay there; hold position; do not move; remain; stand fast; stay; hold |
| Recall (`Recall`) | return to me; back to me; come here; come back; return; recall; disengage and return; come |
| Whistle Recall (`WhistleRecall`) | whistle recall; recall whistle; come on whistle |
| Hand Signal (`HandSignal`) | hand signal; signal recall; silent recall |
| Fetch / Play (`Fetch`) | fetch the ball; retrieve the ball; get the ball; bring the ball; go fetch; play fetch; play ball; retrieve; fetch |
| Search Area (`SearchArea`) | search the area; clear the area; sweep the area; check the area; area search; search around; find the odor; search |
| Search Building (`SearchBuilding`) | search the building; clear the building; building search; clear the rooms; search inside; check the building |
| Search Vehicle (`SearchVehicle`) | search the vehicle; search this vehicle; search the car; check the vehicle; check the car; sniff the vehicle; sweep the vehicle; vehicle search; search vehicle |
| Narcotics Search (`SearchNarcotics`) | search for narcotics; narcotics search; search for drugs; drug search; find the drugs; check for narcotics; narcotics sweep; find dope |
| Explosives Search (`SearchExplosives`) | search for explosives; explosives search; bomb search; search for a bomb; find the bomb; check for explosives; explosive sweep; bomb sweep |
| Weapons Search (`SearchWeapons`) | search for weapons; weapons search; gun search; search for a gun; find the weapon; check for firearms; firearm sweep; weapons sweep |
| Clear Evidence Markers (`ClearEvidenceMarkers`) | clear evidence markers; remove evidence markers; clear k9 markers |
| Collect Scent Article (`CollectScent`) | collect scent article; bag the scent; take scent sample; collect scent; collect scent from vehicle; collect vehicle scent; sample the vehicle scent |
| Track (`Track`) | start tracking; pick up the scent; follow the scent; find the trail; track the suspect; locate them; find him; find her; find them; track scent from vehicle; track from vehicle; track vehicle scent; track the child; track scent; track |
| Reacquire Trail (`FindTrail`) | reacquire the trail; find the trail again; pick the trail back up; recover the scent; find scent; reacquire scent |
| K9 Warning (`K9Warning`) | give k9 warning; give the warning; police k9 warning; announce the dog; warn the suspect; k9 warning |
| Apprehend (`Apprehend`) | apprehend the suspect; engage the suspect; take the suspect; send the dog; attack; bite; get him; get her; take him; take her; apprehend; engage |
| PR/STP Arrest Handoff (`HandoffArrest`) | handoff arrest; start arrest handoff; give suspect to policing menu; process suspect; arrest handoff |
| Request Perimeter (`RequestPerimeter`) | request perimeter; set a perimeter; call perimeter units; containment units |
| K9 Hold Perimeter (`HoldPerimeter`) | hold the perimeter; patrol the perimeter; watch the perimeter; perimeter patrol |
| Contain Suspect (`ContainSuspect`) | contain the suspect; block the suspect; hold the suspect there; keep them contained; contain |
| Save Containment Position (`SaveContainmentPosition`) | save containment position; mark containment position; save this perimeter point |
| Send to Containment Position (`SendContainmentPosition`) | send to containment position; deploy to saved position; take containment position |
| Next Containment Position (`NextContainmentPosition`) | next containment position; move to next perimeter point; cycle containment position |
| Clear Containment Position (`ClearContainmentPosition`) | clear containment position; remove saved perimeter point; delete containment position |
| Request Transport (`RequestTransport`) | request prisoner transport; call transport; prisoner transport; transport suspect |
| Request Medical (`RequestMedical`) | request ems; call ems; request medical; medical assistance |
| Request Bomb Squad (`RequestBombSquad`) | request bomb squad; call bomb squad; request explosive unit; bomb disposal |
| Door Pop (`DoorPop`) | door pop; deploy from vehicle; release from car; pop the door |
| Release / Stop (`Release`) | release the suspect; stop the dog; stop apprehension; disengage; break contact; leave it; let go; release; out |
| Guard (`Guard`) | guard the suspect; watch the suspect; cover him; cover her; hold the suspect; watch him; watch her; stand guard; guard; watch |
| Bark / Alert (`Bark`) | give an alert; sound off; make noise; bark; alert; speak |
| Enter Vehicle (`EnterVehicle`) | enter the vehicle; enter vehicle; load into the car; load up; mount up; get in the vehicle; get in the car; get inside; get in |
| Exit Vehicle (`ExitVehicle`) | exit the vehicle; exit vehicle; unload from the car; dismount; come out; get out of the vehicle; get out of the car; unload; get out |
| Pet (`Pet`) | pet the dog; praise the dog; reward him; reward her; show affection; good dog; pet |
| Treat / Feed (`Feed`) | give the dog a treat; give a treat; reward with a treat; give food; feed the dog; treat; feed |
| Give Water (`Drink`) | give the dog water; give water; water the dog; get a drink; drink water; water break; hydrate; drink |
| Rest K9 (`Rest`) | rest the dog; take a rest; sleep; rest |
| Inspect K9 (`Inspect`) | inspect the dog; check the dog; check status; check injury; check health; medical check; inspect |
| Field First Aid (`FirstAid`) | give first aid; apply first aid; provide treatment; field treatment; treat the injury; treat injury; first aid |
| Carry / Set Down K9 (`CarryK9`) | carry k9; carry the dog; pick up the dog; set down k9; put the dog down |
| Emergency Load K9 (`EmergencyLoadK9`) | emergency load k9; load injured k9; load the injured dog; emergency load |
| Veterinary Transport (`VeterinaryTransport`) | veterinary transport; route to the vet; transport k9 to vet; emergency vet transport |
| Rehabilitation Session (`Rehabilitation`) | rehabilitation session; rehab k9; k9 rehabilitation; recovery session |
| Veterinary Care (`VeterinaryCare`) | go to the vet; veterinary care; vet treatment; visit veterinarian; vet |
| Restock Equipment (`Restock`) | restock equipment; reload k9 gear; replenish supplies; restock |
| Toggle Leash (`ToggleLeash`) | attach the leash; put on the leash; take off the leash; remove the leash; leash on; leash off; attach leash; remove leash; leash |
| K9 Camera (`ToggleCamera`) | activate dog camera; turn on k9 camera; disable dog camera; turn off k9 camera; dog camera; k9 camera; body camera; camera |
| Core Training (`Training`) | go to training; start core training; training ground; begin academy; academy training; core certification course; core training; academy; certification |
| Narcotics Training (`TrainNarcotics`) | start narcotics training; narcotics certification; drug detection training; train for drugs; narcotics academy |
| Explosives Training (`TrainExplosives`) | start explosives training; bomb certification; bomb detection training; train for explosives; explosives academy |
| Weapons Training (`TrainWeapons`) | start weapons training; weapons certification; gun detection training; firearm training; weapons academy |

## Custom aliases

In `Plugins/LSPDFR/AdvancedK9.ini`, add a `[CommandPhrases]` section and use the command key listed above. Separate aliases with `|` or `;`. They supplement built-in phrases. The wake-word requirement still applies. Example:

```ini
[CommandPhrases]
LieDown=settle down|belly down
```

Say **Rex, settle down**. User-added aliases depend on your own INI and are not included in this static reference.

## Localized aliases

English aliases remain available. Only the selected language pack's additional phrases are active. Use **K9** or your configured dog's name with localized phrases too; the command matcher uses these explicit wake words. Below are every built-in additional language alias from the current source.

### Es

| Command | Phrases |
| --- | --- |
| `Follow` | Seguir; sígueme |
| `Heel` | Junto; al pie |
| `Sit` | Sentado; siéntate |
| `LieDown` | Tumbado; échate |
| `Stay` | Quieto; quédate |
| `Recall` | Ven; vuelve |
| `WhistleRecall` | Silbido de regreso |
| `HandSignal` | Señal de mano |
| `Fetch` | Busca la pelota; trae la pelota |
| `SearchArea` | Busca en la zona |
| `SearchBuilding` | Registra el edificio |
| `SearchVehicle` | Registra el vehículo; busca en el coche |
| `SearchNarcotics` | Busca narcóticos; busca drogas |
| `SearchExplosives` | Busca explosivos; busca bombas |
| `SearchWeapons` | Busca armas |
| `CollectScent` | Recoge el olor |
| `Track` | Rastrea; sigue el olor |
| `FindTrail` | Recupera el rastro |
| `K9Warning` | Aviso K9 |
| `Apprehend` | Detén al sospechoso; ataca |
| `HandoffArrest` | Entrega del arresto |
| `RequestPerimeter` | Solicita perímetro |
| `HoldPerimeter` | Mantén el perímetro |
| `ContainSuspect` | Contén al sospechoso |
| `RequestTransport` | Solicita transporte |
| `RequestMedical` | Solicita ambulancia |
| `RequestBombSquad` | Solicita artificieros |
| `DoorPop` | Abre la puerta |
| `Release` | Suelta; déjalo |
| `Guard` | Vigila |
| `Bark` | Ladra |
| `EnterVehicle` | Entra al vehículo; sube al coche |
| `ExitVehicle` | Sal del vehículo; baja del coche |
| `Pet` | Acariciar; buen perro |
| `Feed` | Dar comida; premio |
| `Drink` | Dar agua |
| `Rest` | Descansa |
| `Inspect` | Revisar K9 |
| `FirstAid` | Primeros auxilios |
| `CarryK9` | Cargar K9; recoge al perro |
| `VeterinaryCare` | Cuidado veterinario |
| `Restock` | Reponer equipo |
| `ToggleLeash` | Correa; poner la correa; quitar la correa |
| `ToggleCamera` | Cámara K9 |
| `Training` | Entrenamiento básico |
| `TrainNarcotics` | Entrenamiento de narcóticos |
| `TrainExplosives` | Entrenamiento de explosivos |
| `TrainWeapons` | Entrenamiento de armas |
| `SpawnDismiss` | Desplegar K9; retirar K9 |

### Fr

| Command | Phrases |
| --- | --- |
| `Follow` | Suivre; suis-moi |
| `Heel` | Au pied |
| `Sit` | Assis; assis-toi |
| `LieDown` | Couché |
| `Stay` | Reste; pas bouger |
| `Recall` | Reviens; viens ici |
| `WhistleRecall` | Rappel au sifflet |
| `HandSignal` | Signal de la main |
| `Fetch` | Rapporte; cherche la balle |
| `SearchArea` | Fouille la zone |
| `SearchBuilding` | Fouille le bâtiment |
| `SearchVehicle` | Fouille le véhicule; cherche dans la voiture |
| `SearchNarcotics` | Cherche les stupéfiants; cherche la drogue |
| `SearchExplosives` | Cherche les explosifs; cherche la bombe |
| `SearchWeapons` | Cherche les armes |
| `CollectScent` | Prends l'odeur |
| `Track` | Piste; suis la piste |
| `FindTrail` | Retrouve la piste |
| `K9Warning` | Avertissement K9 |
| `Apprehend` | Appréhende le suspect; attaque |
| `HandoffArrest` | Transfert d'arrestation |
| `RequestPerimeter` | Demande un périmètre |
| `HoldPerimeter` | Tiens le périmètre |
| `ContainSuspect` | Contiens le suspect |
| `RequestTransport` | Demande un transport |
| `RequestMedical` | Demande les secours |
| `RequestBombSquad` | Demande les démineurs |
| `DoorPop` | Ouvre la porte |
| `Release` | Lâche; arrête |
| `Guard` | Garde |
| `Bark` | Aboie |
| `EnterVehicle` | Monte dans le véhicule |
| `ExitVehicle` | Sors du véhicule |
| `Pet` | Caresser; bon chien |
| `Feed` | Nourrir; donne une friandise |
| `Drink` | Donne de l'eau |
| `Rest` | Repose-toi |
| `Inspect` | Examiner K9 |
| `FirstAid` | Premiers secours |
| `CarryK9` | Porter K9; ramasse le chien |
| `VeterinaryCare` | Soins vétérinaires |
| `Restock` | Réapprovisionner |
| `ToggleLeash` | Laisse; mets la laisse; enlève la laisse |
| `ToggleCamera` | Caméra K9 |
| `Training` | Entraînement de base |
| `TrainNarcotics` | Entraînement stupéfiants |
| `TrainExplosives` | Entraînement explosifs |
| `TrainWeapons` | Entraînement armes |
| `SpawnDismiss` | Déploie K9; retire K9 |

### Zh

| Command | Phrases |
| --- | --- |
| `Follow` | 跟随; 跟着我 |
| `Heel` | 靠腿 |
| `Sit` | 坐下 |
| `LieDown` | 趴下 |
| `Stay` | 别动; 待着 |
| `Recall` | 回来; 过来 |
| `WhistleRecall` | 口哨召回 |
| `HandSignal` | 手势指令 |
| `Fetch` | 取回来; 拿球 |
| `SearchArea` | 搜索区域 |
| `SearchBuilding` | 搜索建筑 |
| `SearchVehicle` | 搜索车辆 |
| `SearchNarcotics` | 搜索毒品 |
| `SearchExplosives` | 搜索爆炸物; 搜索炸弹 |
| `SearchWeapons` | 搜索武器 |
| `CollectScent` | 采集气味 |
| `Track` | 追踪; 跟踪气味 |
| `FindTrail` | 重新找到踪迹 |
| `K9Warning` | 警犬警告 |
| `Apprehend` | 抓住嫌疑人; 攻击 |
| `HandoffArrest` | 移交逮捕 |
| `RequestPerimeter` | 请求封锁 |
| `HoldPerimeter` | 守住封锁线 |
| `ContainSuspect` | 控制嫌疑人 |
| `RequestTransport` | 请求押送 |
| `RequestMedical` | 请求医疗 |
| `RequestBombSquad` | 请求排爆组 |
| `DoorPop` | 打开车门 |
| `Release` | 松开; 停止 |
| `Guard` | 警戒 |
| `Bark` | 叫 |
| `EnterVehicle` | 上车 |
| `ExitVehicle` | 下车 |
| `Pet` | 抚摸; 好狗 |
| `Feed` | 喂食; 给奖励 |
| `Drink` | 给水 |
| `Rest` | 休息 |
| `Inspect` | 检查警犬 |
| `FirstAid` | 急救 |
| `CarryK9` | 抱起警犬; 抱起狗 |
| `VeterinaryCare` | 兽医护理 |
| `Restock` | 补充装备 |
| `ToggleLeash` | 牵引绳; 系上牵引绳; 解开牵引绳 |
| `ToggleCamera` | 警犬摄像机 |
| `Training` | 基础训练 |
| `TrainNarcotics` | 毒品训练 |
| `TrainExplosives` | 爆炸物训练 |
| `TrainWeapons` | 武器训练 |
| `SpawnDismiss` | 部署警犬; 收回警犬 |

### Pt

| Command | Phrases |
| --- | --- |
| `Follow` | Seguir; segue-me |
| `Heel` | Junto |
| `Sit` | Senta; sentado |
| `LieDown` | Deita |
| `Stay` | Fica; quieto |
| `Recall` | Volta; vem aqui |
| `WhistleRecall` | Retorno com assobio |
| `HandSignal` | Sinal de mão |
| `Fetch` | Busca; traz a bola |
| `SearchArea` | Procura na área |
| `SearchBuilding` | Revista o prédio |
| `SearchVehicle` | Revista o veículo |
| `SearchNarcotics` | Procura narcóticos; procura drogas |
| `SearchExplosives` | Procura explosivos; procura bomba |
| `SearchWeapons` | Procura armas |
| `CollectScent` | Recolhe o odor |
| `Track` | Rastreia; segue o cheiro |
| `FindTrail` | Recupera a trilha |
| `K9Warning` | Aviso K9 |
| `Apprehend` | Captura o suspeito; ataca |
| `HandoffArrest` | Transferir prisão |
| `RequestPerimeter` | Solicita perímetro |
| `HoldPerimeter` | Mantém o perímetro |
| `ContainSuspect` | Contém o suspeito |
| `RequestTransport` | Solicita transporte |
| `RequestMedical` | Solicita emergência médica |
| `RequestBombSquad` | Solicita esquadrão antibombas |
| `DoorPop` | Abre a porta |
| `Release` | Solta; para |
| `Guard` | Guarda |
| `Bark` | Late |
| `EnterVehicle` | Entra no veículo |
| `ExitVehicle` | Sai do veículo |
| `Pet` | Acariciar; bom cachorro |
| `Feed` | Alimentar; dar petisco |
| `Drink` | Dar água |
| `Rest` | Descansa |
| `Inspect` | Examinar K9 |
| `FirstAid` | Primeiros socorros |
| `CarryK9` | Carregar K9; pega o cachorro |
| `VeterinaryCare` | Cuidados veterinários |
| `Restock` | Repor equipamento |
| `ToggleLeash` | Guia; coloca a guia; tira a guia |
| `ToggleCamera` | Câmera K9 |
| `Training` | Treinamento básico |
| `TrainNarcotics` | Treinamento de narcóticos |
| `TrainExplosives` | Treinamento de explosivos |
| `TrainWeapons` | Treinamento de armas |
| `SpawnDismiss` | Mobiliza K9; dispensa K9 |

### Ru

| Command | Phrases |
| --- | --- |
| `Follow` | Следуй; за мной |
| `Heel` | Рядом; к ноге |
| `Sit` | Сидеть |
| `LieDown` | Лежать |
| `Stay` | Место; стой |
| `Recall` | Ко мне; вернись |
| `WhistleRecall` | Отзыв свистком |
| `HandSignal` | Жест рукой |
| `Fetch` | Апорт; принеси мяч |
| `SearchArea` | Ищи на территории |
| `SearchBuilding` | Обыщи здание |
| `SearchVehicle` | Обыщи машину |
| `SearchNarcotics` | Ищи наркотики |
| `SearchExplosives` | Ищи взрывчатку; ищи бомбу |
| `SearchWeapons` | Ищи оружие |
| `CollectScent` | Возьми запах |
| `Track` | След; ищи по запаху |
| `FindTrail` | Найди след снова |
| `K9Warning` | Предупреждение K9 |
| `Apprehend` | Задержать подозреваемого; фас |
| `HandoffArrest` | Передать задержание |
| `RequestPerimeter` | Запросить оцепление |
| `HoldPerimeter` | Держать периметр |
| `ContainSuspect` | Блокировать подозреваемого |
| `RequestTransport` | Запросить транспорт |
| `RequestMedical` | Вызвать скорую |
| `RequestBombSquad` | Вызвать сапёров |
| `DoorPop` | Открыть дверь |
| `Release` | Отпусти; фу |
| `Guard` | Охраняй |
| `Bark` | Голос |
| `EnterVehicle` | В машину |
| `ExitVehicle` | Из машины |
| `Pet` | Погладить; хороший пёс |
| `Feed` | Кормить; дать лакомство |
| `Drink` | Дать воды |
| `Rest` | Отдыхай |
| `Inspect` | Осмотреть K9 |
| `FirstAid` | Первая помощь |
| `CarryK9` | Нести K9; поднять собаку |
| `VeterinaryCare` | Ветеринарная помощь |
| `Restock` | Пополнить снаряжение |
| `ToggleLeash` | Поводок; надеть поводок; снять поводок |
| `ToggleCamera` | Камера K9 |
| `Training` | Основная тренировка |
| `TrainNarcotics` | Тренировка по наркотикам |
| `TrainExplosives` | Тренировка по взрывчатке |
| `TrainWeapons` | Тренировка по оружию |
| `SpawnDismiss` | Вывести K9; убрать K9 |

### Th

| Command | Phrases |
| --- | --- |
| `Follow` | ตาม; ตามฉันมา |
| `Heel` | ชิดข้าง |
| `Sit` | นั่ง |
| `LieDown` | หมอบ |
| `Stay` | อยู่; อย่าขยับ |
| `Recall` | กลับมา; มานี่ |
| `WhistleRecall` | เรียกกลับด้วยเสียงนกหวีด |
| `HandSignal` | สัญญาณมือ |
| `Fetch` | คาบมา; เอาลูกบอลมา |
| `SearchArea` | ค้นหาพื้นที่ |
| `SearchBuilding` | ค้นหาอาคาร |
| `SearchVehicle` | ค้นหารถ |
| `SearchNarcotics` | ค้นหายาเสพติด |
| `SearchExplosives` | ค้นหาวัตถุระเบิด; ค้นหาระเบิด |
| `SearchWeapons` | ค้นหาอาวุธ |
| `CollectScent` | เก็บกลิ่น |
| `Track` | ติดตาม; ตามกลิ่น |
| `FindTrail` | หากลิ่นอีกครั้ง |
| `K9Warning` | คำเตือน K9 |
| `Apprehend` | จับผู้ต้องสงสัย; โจมตี |
| `HandoffArrest` | ส่งต่อการจับกุม |
| `RequestPerimeter` | ขอกำลังปิดล้อม |
| `HoldPerimeter` | รักษาแนวปิดล้อม |
| `ContainSuspect` | ควบคุมผู้ต้องสงสัย |
| `RequestTransport` | ขอรถควบคุม |
| `RequestMedical` | ขอหน่วยแพทย์ |
| `RequestBombSquad` | ขอหน่วยเก็บกู้ระเบิด |
| `DoorPop` | เปิดประตู |
| `Release` | ปล่อย; หยุด |
| `Guard` | เฝ้า |
| `Bark` | เห่า |
| `EnterVehicle` | ขึ้นรถ |
| `ExitVehicle` | ลงรถ |
| `Pet` | ลูบ; เด็กดี |
| `Feed` | ให้อาหาร; ให้ขนม |
| `Drink` | ให้น้ำ |
| `Rest` | พัก |
| `Inspect` | ตรวจ K9 |
| `FirstAid` | ปฐมพยาบาล |
| `CarryK9` | อุ้ม K9; อุ้มสุนัข |
| `VeterinaryCare` | รักษาสัตวแพทย์ |
| `Restock` | เติมอุปกรณ์ |
| `ToggleLeash` | สายจูง; ใส่สายจูง; ถอดสายจูง |
| `ToggleCamera` | กล้อง K9 |
| `Training` | ฝึกพื้นฐาน |
| `TrainNarcotics` | ฝึกค้นหายาเสพติด |
| `TrainExplosives` | ฝึกค้นหาวัตถุระเบิด |
| `TrainWeapons` | ฝึกค้นหาอาวุธ |
| `SpawnDismiss` | นำ K9 ออก; เก็บ K9 |

### Tr

| Command | Phrases |
| --- | --- |
| `Follow` | Takip et; beni takip et |
| `Heel` | Yanıma gel |
| `Sit` | Otur |
| `LieDown` | Yat |
| `Stay` | Kal; kıpırdama |
| `Recall` | Geri gel; buraya gel |
| `WhistleRecall` | Islıkla çağır |
| `HandSignal` | El işareti |
| `Fetch` | Getir; topu getir |
| `SearchArea` | Alanı ara |
| `SearchBuilding` | Binayı ara |
| `SearchVehicle` | Aracı ara |
| `SearchNarcotics` | Uyuşturucu ara |
| `SearchExplosives` | Patlayıcı ara; bomba ara |
| `SearchWeapons` | Silah ara |
| `CollectScent` | Koku al |
| `Track` | İz sür; kokuyu takip et |
| `FindTrail` | İzi yeniden bul |
| `K9Warning` | K9 uyarısı |
| `Apprehend` | Şüpheliyi yakala; saldır |
| `HandoffArrest` | Tutuklamayı devret |
| `RequestPerimeter` | Çevre güvenliği iste |
| `HoldPerimeter` | Çevreyi tut |
| `ContainSuspect` | Şüpheliyi çevrele |
| `RequestTransport` | Nakil iste |
| `RequestMedical` | Sağlık ekibi iste |
| `RequestBombSquad` | Bomba imha iste |
| `DoorPop` | Kapıyı aç |
| `Release` | Bırak; dur |
| `Guard` | Koru |
| `Bark` | Havla |
| `EnterVehicle` | Araca bin |
| `ExitVehicle` | Araçtan in |
| `Pet` | Sev; aferin |
| `Feed` | Besle; ödül ver |
| `Drink` | Su ver |
| `Rest` | Dinlen |
| `Inspect` | K9 kontrol et |
| `FirstAid` | İlk yardım |
| `CarryK9` | K9 taşı; köpeği kaldır |
| `VeterinaryCare` | Veteriner bakımı |
| `Restock` | Ekipmanı tamamla |
| `ToggleLeash` | Tasma; tasmayı tak; tasmayı çıkar |
| `ToggleCamera` | K9 kamerası |
| `Training` | Temel eğitim |
| `TrainNarcotics` | Uyuşturucu eğitimi |
| `TrainExplosives` | Patlayıcı eğitimi |
| `TrainWeapons` | Silah eğitimi |
| `SpawnDismiss` | K9 görevlendir; K9 geri gönder |

### Vi

| Command | Phrases |
| --- | --- |
| `Follow` | Đi theo; theo tôi |
| `Heel` | Đi sát |
| `Sit` | Ngồi |
| `LieDown` | Nằm xuống |
| `Stay` | Ở yên; đừng di chuyển |
| `Recall` | Quay lại; lại đây |
| `WhistleRecall` | Gọi lại bằng còi |
| `HandSignal` | Ra hiệu tay |
| `Fetch` | Nhặt về; lấy bóng |
| `SearchArea` | Tìm khu vực |
| `SearchBuilding` | Tìm trong tòa nhà |
| `SearchVehicle` | Tìm trong xe |
| `SearchNarcotics` | Tìm ma túy |
| `SearchExplosives` | Tìm chất nổ; tìm bom |
| `SearchWeapons` | Tìm vũ khí |
| `CollectScent` | Lấy mẫu mùi |
| `Track` | Theo dấu; theo mùi |
| `FindTrail` | Tìm lại dấu |
| `K9Warning` | Cảnh báo K9 |
| `Apprehend` | Bắt nghi phạm; tấn công |
| `HandoffArrest` | Bàn giao bắt giữ |
| `RequestPerimeter` | Yêu cầu vành đai |
| `HoldPerimeter` | Giữ vành đai |
| `ContainSuspect` | Khống chế nghi phạm |
| `RequestTransport` | Yêu cầu xe áp giải |
| `RequestMedical` | Yêu cầu y tế |
| `RequestBombSquad` | Yêu cầu đội bom |
| `DoorPop` | Mở cửa |
| `Release` | Thả ra; dừng lại |
| `Guard` | Canh gác |
| `Bark` | Sủa |
| `EnterVehicle` | Lên xe |
| `ExitVehicle` | Xuống xe |
| `Pet` | Vuốt ve; chó ngoan |
| `Feed` | Cho ăn; cho phần thưởng |
| `Drink` | Cho uống nước |
| `Rest` | Nghỉ ngơi |
| `Inspect` | Kiểm tra K9 |
| `FirstAid` | Sơ cứu |
| `CarryK9` | Bế K9; bế chó |
| `VeterinaryCare` | Chăm sóc thú y |
| `Restock` | Bổ sung thiết bị |
| `ToggleLeash` | Dây dắt; đeo dây dắt; tháo dây dắt |
| `ToggleCamera` | Camera K9 |
| `Training` | Huấn luyện cơ bản |
| `TrainNarcotics` | Huấn luyện ma túy |
| `TrainExplosives` | Huấn luyện chất nổ |
| `TrainWeapons` | Huấn luyện vũ khí |
| `SpawnDismiss` | Triển khai K9; cho K9 nghỉ |

### De

| Command | Phrases |
| --- | --- |
| `Follow` | Folgen; folge mir |
| `Heel` | Bei Fuß |
| `Sit` | Sitz |
| `LieDown` | Platz |
| `Stay` | Bleib |
| `Recall` | Komm zurück; hierher |
| `WhistleRecall` | Pfeifenrückruf |
| `HandSignal` | Handzeichen |
| `Fetch` | Hol; bring den Ball |
| `SearchArea` | Such das Gebiet ab |
| `SearchBuilding` | Durchsuche das Gebäude |
| `SearchVehicle` | Durchsuche das Fahrzeug |
| `SearchNarcotics` | Suche Drogen |
| `SearchExplosives` | Suche Sprengstoff; suche Bombe |
| `SearchWeapons` | Suche Waffen |
| `CollectScent` | Nimm die Witterung auf |
| `Track` | Spur suchen; folge der Fährte |
| `FindTrail` | Fährte wiederfinden |
| `K9Warning` | Diensthundwarnung |
| `Apprehend` | Fass den Verdächtigen; fass |
| `HandoffArrest` | Festnahme übergeben |
| `RequestPerimeter` | Sperrkreis anfordern |
| `HoldPerimeter` | Sperrkreis halten |
| `ContainSuspect` | Verdächtigen eindämmen |
| `RequestTransport` | Gefangenentransport anfordern |
| `RequestMedical` | Rettungsdienst anfordern |
| `RequestBombSquad` | Entschärfer anfordern |
| `DoorPop` | Tür öffnen |
| `Release` | Aus; lass los |
| `Guard` | Bewachen |
| `Bark` | Gib Laut |
| `EnterVehicle` | Ins Fahrzeug |
| `ExitVehicle` | Aus dem Fahrzeug |
| `Pet` | Streicheln; braver Hund |
| `Feed` | Füttern; Leckerli geben |
| `Drink` | Wasser geben |
| `Rest` | Ausruhen |
| `Inspect` | K9 untersuchen |
| `FirstAid` | Erste Hilfe |
| `CarryK9` | K9 tragen; Hund aufheben |
| `VeterinaryCare` | Tierärztliche Versorgung |
| `Restock` | Ausrüstung auffüllen |
| `ToggleLeash` | Leine; Leine anlegen; Leine abnehmen |
| `ToggleCamera` | K9-Kamera |
| `Training` | Grundausbildung |
| `TrainNarcotics` | Drogensuchtraining |
| `TrainExplosives` | Sprengstofftraining |
| `TrainWeapons` | Waffensuchtraining |
| `SpawnDismiss` | K9 einsetzen; K9 zurückrufen |

### Cs

| Command | Phrases |
| --- | --- |
| `Follow` | Následuj; za mnou |
| `Heel` | K noze |
| `Sit` | Sedni |
| `LieDown` | Lehni |
| `Stay` | Zůstaň |
| `Recall` | Ke mně; vrať se |
| `WhistleRecall` | Přivolání píšťalkou |
| `HandSignal` | Signál rukou |
| `Fetch` | Aport; přines míček |
| `SearchArea` | Prohledej oblast |
| `SearchBuilding` | Prohledej budovu |
| `SearchVehicle` | Prohledej vozidlo |
| `SearchNarcotics` | Hledej drogy |
| `SearchExplosives` | Hledej výbušniny; hledej bombu |
| `SearchWeapons` | Hledej zbraně |
| `CollectScent` | Načti pach |
| `Track` | Stopuj; sleduj pach |
| `FindTrail` | Najdi stopu znovu |
| `K9Warning` | Varování K9 |
| `Apprehend` | Zadrž podezřelého; zaútoč |
| `HandoffArrest` | Předat zatčení |
| `RequestPerimeter` | Vyžádat perimetr |
| `HoldPerimeter` | Držet perimetr |
| `ContainSuspect` | Zadržet podezřelého |
| `RequestTransport` | Vyžádat převoz |
| `RequestMedical` | Vyžádat záchranku |
| `RequestBombSquad` | Vyžádat pyrotechniky |
| `DoorPop` | Otevři dveře |
| `Release` | Pusť; dost |
| `Guard` | Hlídej |
| `Bark` | Štěkej |
| `EnterVehicle` | Do vozidla |
| `ExitVehicle` | Z vozidla |
| `Pet` | Pohladit; hodný pes |
| `Feed` | Nakrmit; dát pamlsek |
| `Drink` | Dát vodu |
| `Rest` | Odpočívej |
| `Inspect` | Prohlédnout K9 |
| `FirstAid` | První pomoc |
| `CarryK9` | Nést K9; zvednout psa |
| `VeterinaryCare` | Veterinární péče |
| `Restock` | Doplnit výbavu |
| `ToggleLeash` | Vodítko; nasadit vodítko; sundat vodítko |
| `ToggleCamera` | Kamera K9 |
| `Training` | Základní výcvik |
| `TrainNarcotics` | Výcvik drog |
| `TrainExplosives` | Výcvik výbušnin |
| `TrainWeapons` | Výcvik zbraní |
| `SpawnDismiss` | Nasadit K9; odvolat K9 |

### It

| Command | Phrases |
| --- | --- |
| `Follow` | Segui; seguimi |
| `Heel` | Al piede |
| `Sit` | Seduto |
| `LieDown` | Terra |
| `Stay` | Resta; fermo |
| `Recall` | Torna; vieni qui |
| `WhistleRecall` | Richiamo con fischio |
| `HandSignal` | Segnale manuale |
| `Fetch` | Riporta; prendi la palla |
| `SearchArea` | Cerca nell'area |
| `SearchBuilding` | Perquisisci l'edificio |
| `SearchVehicle` | Perquisisci il veicolo |
| `SearchNarcotics` | Cerca stupefacenti; cerca droga |
| `SearchExplosives` | Cerca esplosivi; cerca bomba |
| `SearchWeapons` | Cerca armi |
| `CollectScent` | Prendi l'odore |
| `Track` | Traccia; segui l'odore |
| `FindTrail` | Ritrova la traccia |
| `K9Warning` | Avviso K9 |
| `Apprehend` | Ferma il sospetto; attacca |
| `HandoffArrest` | Passa l'arresto |
| `RequestPerimeter` | Richiedi perimetro |
| `HoldPerimeter` | Mantieni il perimetro |
| `ContainSuspect` | Contieni il sospetto |
| `RequestTransport` | Richiedi trasporto |
| `RequestMedical` | Richiedi soccorso medico |
| `RequestBombSquad` | Richiedi artificieri |
| `DoorPop` | Apri la porta |
| `Release` | Lascia; fermo |
| `Guard` | Sorveglia |
| `Bark` | Abbaia |
| `EnterVehicle` | Entra nel veicolo |
| `ExitVehicle` | Esci dal veicolo |
| `Pet` | Accarezza; bravo cane |
| `Feed` | Nutri; dai premio |
| `Drink` | Dai acqua |
| `Rest` | Riposa |
| `Inspect` | Controlla K9 |
| `FirstAid` | Primo soccorso |
| `CarryK9` | Trasporta K9; solleva il cane |
| `VeterinaryCare` | Cure veterinarie |
| `Restock` | Rifornisci attrezzatura |
| `ToggleLeash` | Guinzaglio; metti il guinzaglio; togli il guinzaglio |
| `ToggleCamera` | Telecamera K9 |
| `Training` | Addestramento base |
| `TrainNarcotics` | Addestramento stupefacenti |
| `TrainExplosives` | Addestramento esplosivi |
| `TrainWeapons` | Addestramento armi |
| `SpawnDismiss` | Schiera K9; congeda K9 |

### Ja

| Command | Phrases |
| --- | --- |
| `Follow` | ついてこい; ついてきて |
| `Heel` | ヒール; 横につけ |
| `Sit` | お座り |
| `LieDown` | 伏せ |
| `Stay` | 待て |
| `Recall` | 来い; 戻れ |
| `WhistleRecall` | 笛で呼び戻す |
| `HandSignal` | ハンドシグナル |
| `Fetch` | 持ってこい; ボールを取れ |
| `SearchArea` | 周辺を捜索 |
| `SearchBuilding` | 建物を捜索 |
| `SearchVehicle` | 車両を捜索 |
| `SearchNarcotics` | 薬物を捜索 |
| `SearchExplosives` | 爆発物を捜索; 爆弾を探せ |
| `SearchWeapons` | 武器を捜索 |
| `CollectScent` | 臭いを取れ |
| `Track` | 追跡; 臭いを追え |
| `FindTrail` | 臭跡を再発見 |
| `K9Warning` | 警察犬警告 |
| `Apprehend` | 容疑者を確保; 攻撃 |
| `HandoffArrest` | 逮捕引き継ぎ |
| `RequestPerimeter` | 包囲を要請 |
| `HoldPerimeter` | 包囲を維持 |
| `ContainSuspect` | 容疑者を封じ込めろ |
| `RequestTransport` | 護送を要請 |
| `RequestMedical` | 救急を要請 |
| `RequestBombSquad` | 爆発物処理班を要請 |
| `DoorPop` | ドアを開けろ |
| `Release` | 離せ; やめ |
| `Guard` | 警戒 |
| `Bark` | 吠えろ |
| `EnterVehicle` | 車に乗れ |
| `ExitVehicle` | 車から降りろ |
| `Pet` | 撫でる; いい子 |
| `Feed` | 餌を与える; ご褒美 |
| `Drink` | 水を与える |
| `Rest` | 休め |
| `Inspect` | 警察犬を確認 |
| `FirstAid` | 応急処置 |
| `CarryK9` | 警察犬を運ぶ; 犬を抱く |
| `VeterinaryCare` | 獣医治療 |
| `Restock` | 装備補充 |
| `ToggleLeash` | リード; リードを付ける; リードを外す |
| `ToggleCamera` | 警察犬カメラ |
| `Training` | 基礎訓練 |
| `TrainNarcotics` | 薬物探知訓練 |
| `TrainExplosives` | 爆発物探知訓練 |
| `TrainWeapons` | 武器探知訓練 |
| `SpawnDismiss` | 警察犬を出す; 警察犬を戻す |

### Pl

| Command | Phrases |
| --- | --- |
| `Follow` | Podążaj; za mną |
| `Heel` | Do nogi |
| `Sit` | Siad |
| `LieDown` | Waruj |
| `Stay` | Zostań |
| `Recall` | Do mnie; wróć |
| `WhistleRecall` | Przywołanie gwizdkiem |
| `HandSignal` | Sygnał ręką |
| `Fetch` | Aport; przynieś piłkę |
| `SearchArea` | Przeszukaj teren |
| `SearchBuilding` | Przeszukaj budynek |
| `SearchVehicle` | Przeszukaj pojazd |
| `SearchNarcotics` | Szukaj narkotyków |
| `SearchExplosives` | Szukaj materiałów wybuchowych; szukaj bomby |
| `SearchWeapons` | Szukaj broni |
| `CollectScent` | Pobierz zapach |
| `Track` | Trop; idź za zapachem |
| `FindTrail` | Odnajdź trop |
| `K9Warning` | Ostrzeżenie K9 |
| `Apprehend` | Zatrzymaj podejrzanego; atakuj |
| `HandoffArrest` | Przekaż zatrzymanie |
| `RequestPerimeter` | Wezwij kordon |
| `HoldPerimeter` | Trzymaj kordon |
| `ContainSuspect` | Powstrzymaj podejrzanego |
| `RequestTransport` | Wezwij transport |
| `RequestMedical` | Wezwij pogotowie |
| `RequestBombSquad` | Wezwij saperów |
| `DoorPop` | Otwórz drzwi |
| `Release` | Puść; zostaw |
| `Guard` | Pilnuj |
| `Bark` | Daj głos |
| `EnterVehicle` | Do pojazdu |
| `ExitVehicle` | Z pojazdu |
| `Pet` | Pogłaszcz; dobry pies |
| `Feed` | Nakarm; daj smakołyk |
| `Drink` | Daj wodę |
| `Rest` | Odpocznij |
| `Inspect` | Sprawdź K9 |
| `FirstAid` | Pierwsza pomoc |
| `CarryK9` | Nieś K9; podnieś psa |
| `VeterinaryCare` | Opieka weterynaryjna |
| `Restock` | Uzupełnij wyposażenie |
| `ToggleLeash` | Smycz; załóż smycz; zdejmij smycz |
| `ToggleCamera` | Kamera K9 |
| `Training` | Szkolenie podstawowe |
| `TrainNarcotics` | Szkolenie narkotykowe |
| `TrainExplosives` | Szkolenie materiałów wybuchowych |
| `TrainWeapons` | Szkolenie broni |
| `SpawnDismiss` | Wypuść K9; odwołaj K9 |
