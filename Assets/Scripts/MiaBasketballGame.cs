using UnityEngine;

namespace MiaCourt
{
    public enum MatchState { Home, Playing, Paused, Result }

    public sealed class MiaBasketballGame : MonoBehaviour
    {
        public MiaCourtAssets assets;
        public MatchState State { get; private set; } = MatchState.Home;
        public CatPlayer[] players;
        public Rigidbody ballBody;
        public TrailRenderer ballTrail;
        public Camera courtCamera;
        public CourtCamera cameraRig;
        public CourtAudio sound;
        public CourtHUD hud;
        public int selectedCharacter;
        public int OpponentIndex => (selectedCharacter + 1) % MiaCourtAssets.CharacterCount;
        public int holder = -1;
        public readonly int[] scores = new int[2];
        public readonly int[] attempts = new int[2];
        public readonly int[] baskets = new int[2];
        public float remaining = BasketballRules.MatchSeconds;
        public float shotClock = BasketballRules.PossessionSeconds;
        public float countdown;
        public float charge;
        public bool charging;
        public bool overtime;
        public string message = "";
        public float messageTime;
        public Color messageColor = Color.white;
        public float lastQuality;
        public int lastShotPoints;
        public int steals;
        public bool challenge;
        public bool IsBuzzerShot => buzzerShot;
        public bool ShotInFlight => shotLive;
        public int BestScore => PlayerPrefs.GetInt("MiaCourt.BestScore", 0);
        public bool StealInRange
        {
            get
            {
                if (players == null || State != MatchState.Playing || countdown > 0 || scorePause > 0)
                    return false;
                if (holder != 1 || pickupDelay > 0) return false;
                return Vector3.Distance(players[0].transform.position, players[1].transform.position) < StealReach;
            }
        }

        float chargeAge;
        float looseAge;
        float pickupDelay;
        float scorePause;
        float aiThink;
        float dribbleSoundAt;
        float matchAge;
        int nextPossession;
        int lastShooter = -1;
        int shotPoints = 2;
        bool shotLive;
        bool buzzerShot;
        bool scoredThisFlight;
        Vector3 previousBall;
        Vector3 aiDirection;
        GameObject[] trajectory;
        const float ChargeSpeed = .78f;
        // A swipe sweeps over a moment rather than testing a single frame, so a steal does not
        // demand frame-perfect timing, and whiffing costs far less than landing one.
        const float ReachSeconds = .30f;
        const float ReachRecovery = .35f;
        const float StealReach = 2.0f;

        void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            Physics.gravity = Vector3.down * BasketballRules.Gravity;
            Time.fixedDeltaTime = 1f / 60f;
            CourtBuilder.Build(this);
            sound = gameObject.AddComponent<CourtAudio>();
            hud = gameObject.AddComponent<CourtHUD>();
            hud.game = this;
            CreatePlayers();
            CreateTrajectory();
            ShowHome();
        }

        void CreatePlayers()
        {
            players = new CatPlayer[2];
            for (int team = 0; team < 2; team++)
                players[team] = new GameObject(team == 0 ? "Human player" : "Computer player").AddComponent<CatPlayer>();
            ApplyCharacters();
        }

        void ApplyCharacters()
        {
            int cpu = OpponentIndex;
            BindPlayer(players[0], selectedCharacter, 0);
            BindPlayer(players[1], cpu, 1);
        }

        void BindPlayer(CatPlayer actor, int roster, int team)
        {
            actor.Initialize(assets.ModelFor(roster), assets.TextureFor(roster), team);
            actor.displayName = MiaCourtAssets.NameFor(roster);
        }

        public void SelectCharacter(int index)
        {
            if (State != MatchState.Home || index < 0 || index >= MiaCourtAssets.CharacterCount) return;
            if (index == selectedCharacter) return;
            selectedCharacter = index;
            ApplyCharacters();
            ShowHome();
            sound.Play(CourtSound.Select);
        }

        public void ShowHome()
        {
            Time.timeScale = 1;
            State = MatchState.Home;
            charging = false;
            messageTime = 0;
            scorePause = 0;
            players[0].transform.position = new Vector3(1.5f,0,-.5f);
            players[1].transform.position = new Vector3(5.0f,0,1.0f);
            foreach (CatPlayer p in players) p.ResetMotion();
            GiveBall(0);
            cameraRig.SetView(1);
            cameraRig.Snap();
            SetTrajectory(false);
        }

        public void StartMatch()
        {
            Time.timeScale = 1;
            State = MatchState.Playing;
            scores[0] = scores[1] = attempts[0] = attempts[1] = baskets[0] = baskets[1] = 0;
            remaining = BasketballRules.MatchSeconds;
            overtime = buzzerShot = false;
            steals = 0;
            matchAge = 0;
            countdown = 3f;
            scorePause = 0;
            aiThink = 0;
            foreach (CatPlayer p in players) { p.ResetMotion(); p.distanceTravelled = 0; }
            ResetPossession(0);
            ShowMessage("往右側籃框進攻", CourtBuilder.Mint, 3.5f);
            sound.Play(CourtSound.Select);
        }

        public void TogglePause()
        {
            if (State == MatchState.Playing)
            {
                State = MatchState.Paused;
                Time.timeScale = 0;
                charging = false;
                SetTrajectory(false);
            }
            else if (State == MatchState.Paused)
            {
                State = MatchState.Playing;
                Time.timeScale = 1;
            }
        }

        public void GiveBall(int team)
        {
            holder = team;
            if (!ballBody.isKinematic) { ballBody.linearVelocity = Vector3.zero; ballBody.angularVelocity = Vector3.zero; }
            ballBody.isKinematic = true;
            ballBody.detectCollisions = false;
            ballTrail.emitting = false;
            shotClock = BasketballRules.PossessionSeconds;
            shotLive = false;
            scoredThisFlight = false;
            lastShooter = -1;
            looseAge = 0;
            pickupDelay = .8f;
            charging = false;
            players[team].possessionAge = 0;
            SetTrajectory(false);
        }

        void ResetPossession(int team)
        {
            players[0].transform.position = new Vector3(-3.4f,0,-.5f);
            players[1].transform.position = new Vector3(3.4f,0,.5f);
            foreach (CatPlayer p in players) p.ResetMotion();
            GiveBall(team);
            scorePause = 0;
        }

        void Update()
        {
            if (players == null) return;
            if (Input.GetKeyDown(KeyCode.F11)) Screen.fullScreen = !Screen.fullScreen;
            if (Input.GetKeyDown(KeyCode.M)) sound.ToggleMute();
            if (State == MatchState.Home)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                    SelectCharacter((selectedCharacter + MiaCourtAssets.CharacterCount - 1) % MiaCourtAssets.CharacterCount);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                    SelectCharacter((selectedCharacter + 1) % MiaCourtAssets.CharacterCount);
                for (int i = 0; i < MiaCourtAssets.CharacterCount && i < 9; i++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) ||
                        Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i)))
                        SelectCharacter(i);
                if (MiaCourtAssets.CharacterCount > 9 &&
                    (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)))
                    SelectCharacter(9);
                if (Input.GetKeyDown(KeyCode.Return)) StartMatch();
                foreach (CatPlayer p in players) p.Animate(Time.deltaTime, p.team == holder, true);
                UpdateHeldBall();
                UpdateMarkers();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (State == MatchState.Result)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.R)) StartMatch();
                return;
            }
            if (State != MatchState.Playing) return;
            float dt = Time.deltaTime;
            messageTime = Mathf.Max(0, messageTime - dt);
            for (int i = 0; i < 3; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) cameraRig.SetView(i);
            if (Input.GetKeyDown(KeyCode.V)) cameraRig.SetView((cameraRig.view + 1) % 3);
            if (countdown > 0)
            {
                countdown -= dt;
                foreach (CatPlayer p in players) p.Animate(dt,p.team == holder,false);
                UpdateHeldBall();
                UpdateMarkers();
                return;
            }
            if (scorePause > 0)
            {
                scorePause -= dt;
                if (scorePause <= 0)
                {
                    if (remaining <= 0) FinishMatch();
                    else ResetPossession(nextPossession);
                }
                return;
            }
            matchAge += dt;
            remaining = Mathf.Max(0,remaining-dt);
            pickupDelay = Mathf.Max(0,pickupDelay-dt);
            if (holder >= 0)
            {
                shotClock = Mathf.Max(0,shotClock-dt);
                if (shotClock <= 0)
                {
                    int team = 1-holder;
                    ResetPossession(team);
                    ShowMessage("進攻時間到 · 交換球權", Color.white, 2);
                }
            }
            if (remaining <= 0)
            {
                if (holder < 0 && shotLive && looseAge < 5 && ballBody.position.y > .5f) buzzerShot = true;
                else { FinishMatch(); return; }
            }
            if (!buzzerShot)
            {
                UpdateHuman(dt);
                UpdateAI(dt);
                SeparatePlayers();
                ResolveReach(dt);
            }
            foreach (CatPlayer p in players) p.Animate(dt,p.team == holder,false);
            UpdateMarkers();
            if (holder >= 0) UpdateHeldBall();
            else UpdateLooseBall(dt);
            if (charging) UpdateTrajectory();
        }

        void UpdateHuman(float dt)
        {
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) -
                      (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            float z = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) -
                      (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            players[0].Move(new Vector3(x,0,z)*(charging?.28f:1f),Input.GetKey(KeyCode.LeftShift),dt);
            bool clickCourt = Input.GetMouseButtonDown(0) && !hud.PointerOverButton();
            if (holder == 0)
            {
                if (!charging && (Input.GetKeyDown(KeyCode.Space) || clickCourt))
                {
                    charging = true;
                    chargeAge = 0;
                    players[0].FaceHoop();
                }
                if (charging)
                {
                    chargeAge += dt;
                    charge = Mathf.PingPong(chargeAge*ChargeSpeed,1f);
                    if (!Input.GetKey(KeyCode.Space) && !Input.GetMouseButton(0)) ReleaseShot(0,charge);
                }
            }
            else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || clickCourt) TryDefense(0);
        }

        void UpdateAI(float dt)
        {
            CatPlayer ai = players[1];
            CatPlayer human = players[0];
            aiThink -= dt;
            if (aiThink <= 0)
            {
                aiThink = challenge ? .12f : .23f;
                Vector3 destination;
                if (holder == 1)
                {
                    destination = new Vector3(-6.3f,0,Mathf.Sin(matchAge*.75f)*2.7f);
                    if (Vector3.Distance(ai.transform.position,human.transform.position) < 2.1f)
                        destination.z = human.transform.position.z > ai.transform.position.z ? -3.8f : 3.8f;
                    float range = Vector3.Distance(ai.transform.position,BasketballRules.HoopFor(1));
                    if (ai.possessionAge > (challenge?2.8f:3.8f) && range < 7.8f)
                    {
                        ReleaseShot(1,Random.Range(challenge?.58f:.48f,challenge?.78f:.88f));
                        aiDirection = Vector3.zero;
                        return;
                    }
                }
                else if (holder == 0)
                {
                    Vector3 towardHoop = BasketballRules.HoopFor(0)-human.transform.position;
                    towardHoop.y = 0;
                    destination = human.transform.position + towardHoop.normalized * (charging?1.05f:1.7f);
                    if (ai.actionCooldown <= 0 && pickupDelay <= 0 && Vector3.Distance(ai.transform.position,human.transform.position)<1.55f)
                    {
                        if (Random.value < (challenge?.65f:.28f)) TryDefense(1);
                        else ai.actionCooldown = 1.4f;
                    }
                }
                else
                {
                    destination = ballBody.position + Vector3.ClampMagnitude(ballBody.linearVelocity*.15f,1.2f);
                    destination.y = 0;
                }
                Vector3 diff = destination-ai.transform.position;
                diff.y = 0;
                aiDirection = diff.magnitude < .20f ? Vector3.zero : diff.normalized;
            }
            ai.Move(aiDirection,false,dt*(challenge?1.08f:.92f));
        }

        void SeparatePlayers()
        {
            Vector3 between = players[0].transform.position-players[1].transform.position;
            float distance = between.magnitude;
            if (distance >= 1.0f) return;
            Vector3 push = (distance < .01f ? Vector3.right : between/distance)*(1f-distance)*.5f;
            players[0].transform.position = BasketballRules.ClampToCourt(players[0].transform.position+push);
            players[1].transform.position = BasketballRules.ClampToCourt(players[1].transform.position-push);
        }

        /// <summary>Starts a swipe. Whether it connects is settled by ResolveReach while it stays live.</summary>
        public void TryDefense(int team)
        {
            CatPlayer actor = players[team];
            if (actor.actionCooldown > 0 || actor.reach > 0) return;
            actor.reach = ReachSeconds;
            actor.actionCooldown = ReachSeconds + ReachRecovery;
            actor.Jump(.62f);   // contest is an all-out jump
        }

        void ResolveReach(float dt)
        {
            for (int team = 0; team < 2; team++)
            {
                CatPlayer actor = players[team];
                if (actor.reach <= 0) continue;
                actor.reach = Mathf.Max(0, actor.reach - dt);
                if (holder == 1-team && pickupDelay <= 0 &&
                    Vector3.Distance(actor.transform.position,players[1-team].transform.position) < StealReach)
                {
                    actor.reach = 0;
                    GiveBall(team);
                    if (team == 0) steals++;
                    ShowMessage(team == 0 ? "抄截成功！" : "球被搶走了，快回防！", team == 0 ? CourtBuilder.Mint : CourtBuilder.Coral,1.6f);
                    sound.Play(CourtSound.Steal);
                }
                else if (holder < 0 && Vector3.Distance(actor.transform.position+Vector3.up*2f,ballBody.position) < 1.6f)
                {
                    actor.reach = 0;
                    ballBody.linearVelocity = new Vector3(team==0?3.5f:-3.5f,5,Random.Range(-2f,2f));
                    shotLive = false;
                    ShowMessage("火鍋！快搶籃板",CourtBuilder.Mint,1.6f);
                    sound.Play(CourtSound.Steal);
                }
            }
        }

        Vector3 ReleasePosition(CatPlayer actor)
        {
            Vector3 direction = BasketballRules.HoopFor(actor.team)-actor.transform.position;
            direction.y = 0;
            return actor.transform.position + Vector3.up*2.5f + direction.normalized*.50f;
        }

        public void ReleaseShot(int team, float releaseCharge)
        {
            if (holder != team || State != MatchState.Playing || countdown > 0) return;
            CatPlayer actor = players[team];
            Vector3 target = BasketballRules.HoopFor(team);
            Vector3 from = ReleasePosition(actor);
            float quality = BasketballRules.ReleaseQuality(releaseCharge);
            float error = quality >= .90f ? .015f : Mathf.Pow(1f-quality,1.5f)*3.9f;
            Vector3 defender = players[1-team].transform.position-actor.transform.position;
            if (defender.magnitude<1.7f && Vector3.Dot(defender.normalized,(target-actor.transform.position).normalized)>.5f)
                error += .32f;
            float phase = attempts[team]*2.39996f+team*.7f;
            target += new Vector3(Mathf.Cos(phase)*error,0,Mathf.Sin(phase)*error);
            holder = -1;
            ballBody.isKinematic = false;
            ballBody.detectCollisions = true;
            ballBody.position = from;
            ballBody.linearVelocity = BasketballRules.LaunchVelocity(from,target,5.0f+Vector3.Distance(from,target)*.08f);
            ballBody.angularVelocity = new Vector3(0,0,team==0?-8f:8f);
            previousBall = from;
            ballTrail.Clear();
            ballTrail.emitting = true;
            shotLive = true;
            scoredThisFlight = false;
            lastShooter = team;
            shotPoints = BasketballRules.ShotValue(actor.transform.position,BasketballRules.HoopFor(team));
            looseAge = 0;
            pickupDelay = .75f;
            charging = false;
            attempts[team]++;
            actor.FaceHoop();
            actor.Jump(.55f);   // jump shot
            SetTrajectory(false);
            sound.Play(CourtSound.Shot);
            if (team == 0)
            {
                lastQuality = quality;
                lastShotPoints = shotPoints;
                ShowMessage(quality >= .90f ? "完美出手！" : quality >= .67f ? "漂亮的出手" : releaseCharge<.68f ? "出手太早" : "出手太晚",
                    quality>=.90f?CourtBuilder.Mint:Color.white,1.5f);
            }
        }

        void FixedUpdate()
        {
            if (State != MatchState.Playing || ballBody == null || holder >= 0 || scoredThisFlight) return;
            Vector3 current = ballBody.position;
            for (int team = 0; team < 2; team++)
            {
                if (BasketballRules.CrossedHoop(previousBall,current,BasketballRules.HoopFor(team)))
                {
                    RegisterBasket(team,lastShooter == team ? shotPoints : 2);
                    break;
                }
            }
            previousBall = current;
        }

        public void RegisterBasket(int team, int points)
        {
            if (scoredThisFlight || State != MatchState.Playing) return;
            scoredThisFlight = true;
            shotLive = false;
            scores[team] += points;
            baskets[team]++;
            nextPossession = 1-team;
            scorePause = 1.5f;
            charging = false;
            SetTrajectory(false);
            ShowMessage(players[team].displayName + (points == 3 ? " · 三分命中！" : " · 得分！"),team==0?CourtBuilder.Mint:CourtBuilder.Coral,2.3f);
            sound.Play(CourtSound.Score);
        }

        void UpdateLooseBall(float dt)
        {
            looseAge += dt;
            if (scorePause > 0 || buzzerShot) return;
            if (ballBody.position.y < 1.65f && pickupDelay <= 0)
            {
                int closest = -1;
                float best = 1.18f;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 flat = ballBody.position; flat.y = 0;
                    float distance = Vector3.Distance(players[i].transform.position,flat);
                    if (distance < best) { best = distance; closest = i; }
                }
                if (closest >= 0)
                {
                    GiveBall(closest);
                    ShowMessage(closest==0?"拿到籃板，繼續進攻！":"對手拿到籃板",closest==0?CourtBuilder.Mint:CourtBuilder.Coral,1.3f);
                }
            }
            if (ballBody.position.y < -2 || Mathf.Abs(ballBody.position.x)>15 || Mathf.Abs(ballBody.position.z)>9 || looseAge > 9f)
            {
                ResetPossession(lastShooter >= 0 ? 1-lastShooter : 0);
                ShowMessage("重新發球",Color.white,1.3f);
            }
        }

        void UpdateHeldBall()
        {
            if (holder < 0) return;
            CatPlayer actor = players[holder];
            Vector3 direction = State == MatchState.Home ? new Vector3(-.3f,0,-1) :
                (actor.movement.sqrMagnitude>.2f ? actor.movement.normalized : new Vector3(holder==0?1:-1,0,0));
            float bounce = charging ? 1.7f : .28f+Mathf.Abs(Mathf.Sin(Time.time*8.3f))*.96f;
            ballBody.position = actor.transform.position+direction*.69f+Vector3.up*(bounce+actor.jump);
            ballBody.transform.Rotate(75*Time.deltaTime,40*Time.deltaTime,10*Time.deltaTime,Space.World);
            if (!charging && Time.time >= dribbleSoundAt && State == MatchState.Playing)
            {
                dribbleSoundAt = Time.time+.38f;
                sound.Play(CourtSound.Dribble,.32f);
            }
        }

        void UpdateMarkers()
        {
            for (int i = 0; i < players.Length; i++)
            {
                players[i].selectionRing.SetActive(i == 0 || holder == i);
                var line = players[i].selectionRing.GetComponent<LineRenderer>();
                line.sharedMaterial.color = i == 0 ? CourtBuilder.Mint : CourtBuilder.Coral;
            }
        }

        void CreateTrajectory()
        {
            trajectory = new GameObject[22];
            Material mat = CourtBuilder.Material("Shot guide",new Color(.78f,1f,.85f));
            mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",new Color(.25f,.45f,.30f));
            for (int i = 0; i < trajectory.Length; i++)
            {
                trajectory[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                trajectory[i].name = "Trajectory dot";
                Destroy(trajectory[i].GetComponent<Collider>());
                trajectory[i].transform.localScale = Vector3.one*.085f;
                trajectory[i].GetComponent<Renderer>().sharedMaterial = mat;
                trajectory[i].GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trajectory[i].SetActive(false);
            }
        }

        void SetTrajectory(bool visible)
        {
            if (trajectory == null) return;
            foreach (var dot in trajectory) dot.SetActive(visible);
        }

        void UpdateTrajectory()
        {
            SetTrajectory(true);
            Vector3 from = ReleasePosition(players[0]);
            Vector3 to = BasketballRules.HoopFor(0);
            Vector3 velocity = BasketballRules.LaunchVelocity(from,to,5+Vector3.Distance(from,to)*.08f);
            Vector3 flat = to-from; flat.y = 0;
            Vector3 planarVelocity = velocity; planarVelocity.y = 0;
            float duration = flat.magnitude/Mathf.Max(.1f,planarVelocity.magnitude);
            for (int i = 0; i < trajectory.Length; i++)
            {
                float t = duration*(i+1)/trajectory.Length;
                trajectory[i].transform.position = from+velocity*t+Physics.gravity*.5f*t*t;
            }
        }

        public void FinishMatch()
        {
            if (scores[0] == scores[1])
            {
                overtime = true;
                buzzerShot = false;
                remaining = 30;
                countdown = 2;
                ResetPossession(scores[0]%2);
                ShowMessage("平手！加賽 30 秒",CourtBuilder.Mint,3);
                sound.Play(CourtSound.Buzzer);
                return;
            }
            State = MatchState.Result;
            Time.timeScale = 1;
            charging = false;
            SetTrajectory(false);
            if (!ballBody.isKinematic) { ballBody.linearVelocity = Vector3.zero; ballBody.angularVelocity = Vector3.zero; }
            ballBody.isKinematic = true;
            ballTrail.emitting = false;
            if (scores[0] > BestScore) { PlayerPrefs.SetInt("MiaCourt.BestScore",scores[0]); PlayerPrefs.Save(); }
            sound.Play(CourtSound.Buzzer);
        }

        public void ShowMessage(string text, Color color, float seconds)
        {
            message = text; messageColor = color; messageTime = seconds;
        }

        void OnDestroy() { Time.timeScale = 1; }
    }
}
