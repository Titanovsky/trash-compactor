# trash-compactor
s&amp;box проект.

## Рабочие системы

- **Gameplay (`code/Gameplay.cs`)**: компонент-синглтон, инициализирующий игровой цикл и вызывающий спавн локального игрока через `Rpc.Broadcast` (HostOnly). Часть логики ролей закомментирована.
- **Player (`code/Player/Player.cs`)**: синглтон `Player.Local`, хранит `Role`, синхронизирует `RoleEnum` и `Name`, реализует `IDamageable`, выполняет спавн в случайной точке роли с `Jump`-воркэраундом против застревания.
- **FpPlayerGrabber (`code/Player/FpPlayerGrabber.cs`)**: физический грэббер от первого лица — захват и перенос тел через `PhysicsBody.SmoothMove` по `attack1`, импульсный пуш с уроном по рейкасту на `attack2`, эффекты попадания и декали.
- **Roles (`code/Role/*`)**: абстрактный `Role` со списком `Spawns` и проверками по типу/строке; реализации `Trashman`, `Survival`, `Spectator` плюс enum `RoleTrashCompactor` для сетевой синхронизации.
- **RoundManager (`code/RoundManager/RoundManager.cs`)**: серверный конечный автомат раундов (`RoundState`) с `TimeUntil` таймером, синхронизацией времени по RPC (`RequestSyncToHostRpc`/`SendSyncToClientsRpc`) и автоциклом Start↔Finish; респавн игрока по окончании раунда.
- **Trash (`code/Trash/Trash.cs`, `SpawnerTrash.cs`)**: мусор как `ICollisionListener`, наносящий игроку урон пропорционально скорости `Rigidbody` при столкновении; есть спавнер мусора.
- **NpcSurvivor (`code/NpcSurvivor.cs`)**: сетевой бот-выживший. На хосте бродит по navmesh через `NavMeshAgent.MoveTo( Scene.NavMesh.GetRandomPoint( WorldPosition, WanderRadius ) )` с рандомной задержкой из `WanderIntervalMin/Max`; анимация citizen-графа задаётся через `Renderer.Set` и репликуется `Rpc.Broadcast`; реализует `IDamageable` и при смерти уничтожает агента, превращаясь в труп с `Rigidbody` и импульсом в точку удара.
- **NpcManager (`code/NpcManager.cs`)**: host-only синглтон, спавнящий от 1 до `MaxNpcCount` ботов на `MapInfo.SpawnSurvivals` с рандомными никами из `NpcNames`; каждый бот получает `NetworkOrphaned.Host` и `NetworkSpawn()`. Чистит ботов и трупы при новом раунде, смене карты и голосовании.
- **MapInfo (`code/Map/MapInfo.cs`)**: синглтон-компонент карты со списками точек спавна для `Trashman`/`Survival`/`Spectator`.
- **UI (`code/UI/Hud.razor`, `ScoreMenu.razor`)**: Razor-HUD с именем игрока, состоянием раунда, таймером, HP/Armor; отдельное меню счёта.
