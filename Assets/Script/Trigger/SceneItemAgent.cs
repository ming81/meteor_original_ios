﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
//using DG.Tweening;
//damage为物件是否可以伤害玩家角色,需要des文件里带伤害值
//damagevalue为物件是否可以被玩家伤害,并且每次伤害值是多少.
//collision为物件是否可以阻塞角色
//active为物件是否激活
//pose x y为播放ID为x的动画片段，并依据y设定的值决定是否循环.
[System.Serializable]
public class SceneItemProperty
{
    //作为默认场景物品才有.
    public Dictionary<string, int> names = new Dictionary<string, int>();
    public Dictionary<string, int> attribute = new Dictionary<string, int>();//是否激活active 是否有伤害(damage) 是否有碰撞(collision) pose做动作.
}
//原版内不需要序列化存储的机关，关卡固有机关,尖刺,摆斧
public class SceneItemAgent : MonoBehaviour {
    // Use this for initialization
    List<Collider> collisions = new List<Collider>();
    //public bool registerCollision;
    public int InstanceId;//从0序号的实例ID
    FMCPlayer player;
    System.Reflection.MethodInfo MethodOnAttack;
    System.Reflection.MethodInfo MethodOnIdle;
    System.Func<int, int, int, int> OnAttackCallBack;
    System.Action<int, int> OnIdle;
    float initializeY;
    bool billBoard = false;
    //场景初始化调用，或者爆出物品，待物品落地时调用
    public void OnStart(LevelScriptBase script = null)
    { 
        initializeY = transform.position.y;
        //自转+高度转
        if (!property.names.ContainsKey("machine") && gameObject.activeSelf && !billBoard)
            StartCoroutine(yMove());
        if (script != null)
        {
            MethodOnAttack = script.GetType().GetMethod(gameObject.name + "_OnAttack");
            MethodOnIdle = script.GetType().GetMethod(gameObject.name + "_OnIdle");
        }
        if (MethodOnAttack != null || OnAttackCallBack != null)
            RefreshCollision();
    }

    bool up = true;
    float yHeight = 5.0f;
    IEnumerator yMove()
    {
        while (true)
        {
            if (up)
            {
                transform.position = new Vector3(transform.position.x, transform.position.y + 5 * Time.deltaTime, transform.position.z);
                if (transform.position.y >= initializeY + yHeight)
                    up = false;
            }
            else
            {
                transform.position = new Vector3(transform.position.x, transform.position.y - 5 * Time.deltaTime, transform.position.z);
                if (transform.position.y <= initializeY - yHeight)
                    up = true;
            }
            transform.Rotate(new Vector3(0, 90 * Time.deltaTime, 0));
            yield return 0;
        }
    }

    private void Awake()
    {
        root = transform;
        Refresh = false;
    }

    void Start () {
    }

    float refresh_tick;
    bool Refresh;
	void Update () {
        if (MethodOnIdle != null)
            MethodOnIdle.Invoke(GameBattleEx.Instance.Script, new object[] { InstanceId });
        else if (OnIdle != null)
            OnIdle.Invoke(InstanceId, Index);
        if (ItemInfo != null && (ItemInfo.IsItem() || ItemInfo.IsWeapon()) && Refresh)
        {
            refresh_tick -= Time.deltaTime;
            if (refresh_tick <= 0.0f)
            {
                OnRefresh();
                Refresh = false;
                if (ItemInfo.IsWeapon() || ItemInfo.IsItem())
                    refresh_tick = ItemInfo.first[1].flag[1];
            }
        }
    }

    public void OnPickup(MeteorUnit unit)
    {
        //场景上的模型物件捡取
        if (unit.Dead)
            return;
        if (ItemInfo != null && root != null)
        {
            if (ItemInfo.IsWeapon())
            {
                //满武器，不能捡
                if (unit.Attr.Weapon != 0 && unit.Attr.Weapon2 != 0)
                    return;
                //相同武器，不能捡
                ItemBase ib0 = GameData.FindItemByIdx(unit.Attr.Weapon);
                WeaponBase wb0 = WeaponMng.Instance.GetItem(ib0.UnitId);
                if (wb0 != null && wb0.WeaponR == ItemInfo.model)
                    return;

                if (unit.Attr.Weapon2 != 0)
                {
                    ItemBase ib1 = GameData.FindItemByIdx(unit.Attr.Weapon2);
                    WeaponBase wb1 = WeaponMng.Instance.GetItem(ib1.UnitId);
                    if (wb1 != null && wb1.WeaponR == ItemInfo.model)
                        return;
                }

                //同类武器不能捡
                int weaponPickup = GameData.GetWeaponCode(ItemInfo.model);
                ItemBase wb = GameData.FindItemByIdx(weaponPickup);
                if (wb == null)
                    return;

                ItemBase wbl = GameData.FindItemByIdx(unit.Attr.Weapon);
                if (wbl == null)
                    return;

                ItemBase wbr = GameData.FindItemByIdx(unit.Attr.Weapon2);
                if (wb.SubType == wbl.SubType)
                    return;

                if (wbr != null && wb.SubType == wbr.SubType)
                    return;
                //可以捡取
                unit.Attr.Weapon2 = weaponPickup;
                SFXLoader.Instance.PlayEffect(672, unit.gameObject, true);
                refresh_tick = ItemInfo.first[1].flag[1];
                if (asDrop)
                    GameObject.Destroy(gameObject);
                else
                {
                    if (refresh_tick > 0)
                        Refresh = true;
                    GameObject.Destroy(root.gameObject);
                    root = null;
                }
            }
            else if (ItemInfo.IsItem())
            {
                //表明此为Buff,会叠加某个属性，且挂上一个持续的特效。
                unit.GetItem(ItemInfo);
                refresh_tick = ItemInfo.first[1].flag[1];
                if (refresh_tick > 0)
                    Refresh = true;
                GameObject.Destroy(root.gameObject);
                root = null;
            }
            else if (ItemInfo.IsFlag())
            {
                GameObject.Destroy(root.gameObject);
                root = null;
                if (ItemInfo.first[1].flag[1] != 0)
                    SFXLoader.Instance.PlayEffect(ItemInfo.first[1].flag[1], unit.gameObject, false);
                U3D.InsertSystemMsg(unit.name + " 夺得镖物");
                unit.SetFlag(ItemInfo, ItemInfo.first[2].flag[1]);
            }
        }
        else if (ItemInfoEx != null)
        {
            if (ItemInfoEx.MainType == 1)
            {
                string weaponModel = "";
                WeaponBase wp = WeaponMng.Instance.GetItem(ItemInfoEx.UnitId);
                if (wp != null)
                    weaponModel = wp.WeaponR;
                //满武器，不能捡
                if (unit.Attr.Weapon != 0 && unit.Attr.Weapon2 != 0)
                    return;
                //相同武器，不能捡
                ItemBase ib0 = GameData.FindItemByIdx(unit.Attr.Weapon);
                WeaponBase wb0 = WeaponMng.Instance.GetItem(ib0.UnitId);
                if (wb0 != null && wb0.WeaponR == weaponModel)
                    return;

                if (unit.Attr.Weapon2 != 0)
                {
                    ItemBase ib1 = GameData.FindItemByIdx(unit.Attr.Weapon2);
                    WeaponBase wb1 = WeaponMng.Instance.GetItem(ib1.UnitId);
                    if (wb1 != null && wb1.WeaponR == weaponModel)
                        return;
                }

                //同类武器不能捡
                int weaponPickup = GameData.GetWeaponCode(weaponModel);
                ItemBase wb = GameData.FindItemByIdx(weaponPickup);
                if (wb == null)
                    return;

                ItemBase wbl = GameData.FindItemByIdx(unit.Attr.Weapon);
                if (wbl == null)
                    return;

                ItemBase wbr = GameData.FindItemByIdx(unit.Attr.Weapon2);
                if (wb.SubType == wbl.SubType)
                    return;

                if (wbr != null && wb.SubType == wbr.SubType)
                    return;
                //可以捡取
                unit.Attr.Weapon2 = weaponPickup;
                SFXLoader.Instance.PlayEffect(672, unit.gameObject, true);
                
                if (asDrop)
                    GameObject.Destroy(gameObject);
            }
        }
    }

    void OnRefresh()
    {
        //只要是刷新，那么根一定是子节点。否则人物接到的时候，就删除了。
        if (root != null)
            GameObject.Destroy(root.gameObject);
        root = new GameObject("root").transform;
        root.SetParent(transform);
        root.gameObject.layer = gameObject.layer;
        root.localScale = Vector3.one;
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        if (ItemInfo != null)
        {
            Load(ItemInfo.model);
            ApplyPost();
        }
        if (ItemInfo.IsFlag())
            U3D.InsertSystemMsg("镖物重置");
    }

    public Transform root;
    public Option ItemInfo;
    public ItemBase ItemInfoEx;
    void ApplyPrev(Option op)
    {
        if (ItemInfo == null)
            ItemInfo = op;
        //如果是物品，那么是物品生成器，会一段时间刷新一个。如果刷新时间小于0，那么不刷.
        if (ItemInfo.IsItem() || ItemInfo.IsFlag() || ItemInfo.IsWeapon())
        {
            root = new GameObject("root").transform;
            root.SetParent(transform);
            root.gameObject.layer = gameObject.layer;
            root.localScale = Vector3.one;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
        }
    }

    public void ApplyPost()
    {
        if (ItemInfo == null || root == null)
        {
            if (ItemInfoEx != null)
            {
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    Collider co = root.transform.GetChild(i).GetComponent<Collider>();
                    if (co != null && co.enabled)
                    {
                        if (co is MeshCollider)
                            (co as MeshCollider).convex = true;
                        co.isTrigger = true;
                    }
                }
            }
            return;
        }
        if (ItemInfo.IsFlag() || ItemInfo.IsItem() || ItemInfo.IsWeapon())
        {
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Collider co = root.transform.GetChild(i).GetComponent<Collider>();
                if (co != null && co.enabled)
                {
                    if (co is MeshCollider)
                        (co as MeshCollider).convex = true;
                    co.isTrigger = true;
                }
            }
        }
    }
    public void Load(string file)
    {
        string s = file;
        if (!string.IsNullOrEmpty(file))
        {
            //查看此物体属于什么，A：武器 B：道具 C：镖物
            if (ItemInfo == null)
            {
                for (int i = 0; i < MenuResLoader.Instance.Info.Count; i++)
                {
                    if (MenuResLoader.Instance.Info[i].model != "0" && 0 == string.Compare(MenuResLoader.Instance.Info[i].model, s, true))
                    {
                        ApplyPrev(MenuResLoader.Instance.Info[i]);
                        break;
                    }
                    string rh = s.ToUpper();
                    string rh2 = MenuResLoader.Instance.Info[i].model.ToUpper();
                    if (rh2.StartsWith(rh))
                    {
                        s = MenuResLoader.Instance.Info[i].model;
                        ApplyPrev(MenuResLoader.Instance.Info[i]);
                        break;
                    }
                }
                //不是一个Meteor.res里的物件
                if (ItemInfo == null)
                {
                    List<ItemBase> its = GameData.itemMng.GetFullRow();
                    for (int i = 0; i < its.Count; i++)
                    {
                        if (its[i].MainType == 1)
                        {
                            WeaponBase weapon = WeaponMng.Instance.GetItem(its[i].UnitId);
                            if (weapon.WeaponR == s)
                            {
                                ItemInfoEx = its[i];
                                break;
                            }
                        }
                    }
                }
            }

            //证明此物品不是可拾取物品
            gameObject.layer = (ItemInfo != null || ItemInfoEx != null) ?  LayerMask.NameToLayer("Trigger") : LayerMask.NameToLayer("Scene");
            root.gameObject.layer = gameObject.layer;
            //箱子椅子桌子酒坛都不允许为场景物品.
            WsGlobal.ShowMeteorObject(s, root);
            DesFile fIns = DesLoader.Instance.Load(s);
            //把子物件的属性刷到一起.
            for (int i = 0; i < fIns.SceneItems.Count; i++)
                LoadCustom(fIns.SceneItems[i].name, fIns.SceneItems[i].custom);
            
            player = GetComponent<FMCPlayer>();
            if (player == null)
                player = gameObject.AddComponent<FMCPlayer>();
            player.Init(s);
            if (player.frames == null)
            {
                Destroy(player);
                player = null;
            }
        }
    }

    public void LoadCustom(string na, List<string> custom_feature)
    {
        for (int i = 0; i < custom_feature.Count; i++)
        {
            string[] kv = custom_feature[i].Split(new char[] { '=' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (kv.Length == 2)
            {
                string k = kv[0].Trim(new char[] { ' ' });
                string v = kv[1].Trim(new char[] { ' ' });
                if (k == "name")
                {
                    string[] varray = v.Split(new char[] { '\"' }, System.StringSplitOptions.RemoveEmptyEntries);
                    string value = varray[0].Trim(new char[] { ' ' });
                    if (value.StartsWith("damage"))
                    {
                        value = value.Substring(6);
                        property.names["damage"] = int.Parse(value);
                    }
                }
                else if (k == "model")
                {

                }
                else if (k == "ticket")
                {

                }
                else if (k == "visible")
                {
                    GameObject obj = Global.ldaControlX(na, root.gameObject);
                    MeshRenderer mr = obj.GetComponent<MeshRenderer>();
                    if (mr != null)
                        mr.enabled = (int.Parse(v) == 1);
                }
                else if (k == "collision")
                {
                    GameObject obj = Global.ldaControlX(na, root.gameObject);
                    Collider[] co = obj.GetComponentsInChildren<Collider>(true);
                    int vv = int.Parse(v);
                    for (int q = 0; q < co.Length; q++)
                    {
                        MeshCollider c = co[q] as MeshCollider;
                        if (c != null)
                            c.convex = vv == 1;
                        co[q].isTrigger = vv == 0;
                        //co[q].enabled = vv == 1;
                    }
                    property.attribute["collision"] = vv;
                }
                else if (k == "blockplayer")
                {

                }
                else if (k == "billboard")
                {
                    LookAtCamara cam = GetComponent<LookAtCamara>();
                    if (cam == null)
                        gameObject.AddComponent<LookAtCamara>();
                    billBoard = true;
                }
                else if (k == "effect")
                {

                }
                else
                    Debug.LogError(na + " can not alysis:" + custom_feature[i]);
            }
        }
    }
    public SceneItemProperty property = new SceneItemProperty();
    public void SetSceneItem(string features, int value)
    {
        if (features == "frame")
        {
            if (player != null)
            {
                player.ChangeFrame(value);
            }
        }
        else
            Debug.LogError("setSceneItem:" + features + " v1:" + value);
    }

    public void SetSceneItem(string features, string value)
    {
        if (features == "name")
        {
            property.names[value] = 1;
            if (value == "damage")
            {

            }
            if (value == "machine")
            {

            }
        }
    }
    public void SetSceneItem(string features, string sub_features, int value)
    {
        if (features == "attribute")
        {
            property.attribute[sub_features] = value;
            if (sub_features == "damage")
            {
                if (value == 1)
                {
                    if (!property.attribute.ContainsKey("collision"))
                    {
                        Collider[] co = GetComponentsInChildren<Collider>();
                        for (int i = 0; i < co.Length; i++)
                        {
                            co[i].enabled = true;
                            if (co[i] as MeshCollider != null)
                                (co[i] as MeshCollider).convex = true;
                            co[i].isTrigger = true;
                        }
                    }
                    else
                    if (property.attribute.ContainsKey("collision") && property.attribute["collision"] == 0)
                    {
                        Collider[] co = GetComponentsInChildren<Collider>();
                        for (int i = 0; i < co.Length; i++)
                        {
                            co[i].enabled = true;
                            if (co[i] as MeshCollider != null)
                                (co[i] as MeshCollider).convex = true;
                            co[i].isTrigger = true;
                        }
                    }
                }
                else
                {

                }
            }
            else if (sub_features == "collision")
            {
                SetCollision(value == 1);
            }
            else if (sub_features == "damagevalue")
            {
            }
            else if (sub_features == "active")
            {
                root.gameObject.SetActive(value != 0);
                //删除受击框
                if (value == 0)
                    GameBattleEx.Instance.RemoveCollision(this);
                if (OnIdle != null && value == 0)
                {
                    MeteorManager.Instance.OnDestroySceneItem(this);
                    GameObject.Destroy(gameObject);
                }
            }
            else if (sub_features == "interactive")
            {
                //与其他不发生交互，无法接触
                Collider[] co = GetComponentsInChildren<Collider>();
                for (int i = 0; i < co.Length; i++)
                    co[i].enabled = value != 0;
                //交互切换
                if (MethodOnAttack != null || OnAttackCallBack != null)
                {
                    if (value == 0)
                        GameBattleEx.Instance.RemoveCollision(this);
                    else if (value == 1)
                    {
                        //刷新受击框.
                        GameBattleEx.Instance.RemoveCollision(this);
                        RefreshCollision();
                        GameBattleEx.Instance.RegisterCollision(this);
                    }
                }
            }
        }
        else if (features == "name")
        {
            property.names[sub_features] = value;
            if (sub_features == "damage")
            {

            }
            if (sub_features == "machine")
            {

            }
        }
    }

    //setSceneItem("xx", "pos", posid, loop)
    public void SetSceneItem(string features, int value1, int value2)
    {
        if (features == "pose")
        {
            if (player != null)
            {
                player.ChangePose(value1, value2);
            }
        }
        else
            Debug.LogError("setSceneItem:" + features + " v1:" + value1 + " v2:" + value2);
    }

    //这个对象是否拥有击伤角色的能力.
    public bool HasDamage()
    {
        if (property.attribute.ContainsKey("damage") && property.attribute["damage"] == 1)
            return true;
        return false;
    }

    //这个对象的击伤能力值
    public int DamageValue()
    {
        if (HasDamage())
        {
            if (property.attribute.ContainsKey("damagevalue"))
                return property.attribute["damagevalue"] / 10;
            else if (property.names.ContainsKey("damage"))
                return property.names["damage"] / 10;
        }
        return 0;
    }

    //受击框
    public void RefreshCollision()
    {
        //BoxCollider box = GetComponent<BoxCollider>();
        collisions.Clear();
        if (MethodOnAttack != null || OnAttackCallBack != null)
        {
            Collider[] co = GetComponentsInChildren<Collider>();
            for (int i = 0; i < co.Length; i++)
            {
                //不显示出来的都没有受击框
                MeshRenderer mr = co[i].GetComponent<MeshRenderer>();
                if (mr != null && mr.enabled)
                    collisions.Add(co[i]);
            }
        }
        //AABBVector bound = new AABBVector();
        //if (co.Length > 1)
        //{
        //    bound.min = co[0].bounds.min;
        //    bound.max = co[0].bounds.max;
        //}
        //for (int i = 1; i < co.Length; i++)
        //{
        //    if (bound.min.x > co[i].bounds.min.x)
        //        bound.min.x = co[i].bounds.min.x;
        //    if (bound.min.y > co[i].bounds.min.y)
        //        bound.min.y = co[i].bounds.min.y;
        //    if (bound.min.z > co[i].bounds.min.z)
        //        bound.min.z = co[i].bounds.min.z;
        //    if (bound.max.x < co[i].bounds.max.x)
        //        bound.max.x = co[i].bounds.max.x;
        //    if (bound.max.y < co[i].bounds.max.y)
        //        bound.max.y = co[i].bounds.max.y;
        //    if (bound.max.z < co[i].bounds.max.z)
        //        bound.max.z = co[i].bounds.max.z;
        //}
        //if (box == null)
        //    box = gameObject.AddComponent<BoxCollider>();
        //box.center = (bound.max - bound.min) / 4;
        //box.size = bound.max - bound.min;
        //box.isTrigger = true;
    }

    //场景物件按照0点防御来算.
    int CalcDamage(MeteorUnit attacker)
    {
        //(((武器攻击力 + buff攻击力) x 招式攻击力） / 100) - （敌方武器防御力 + 敌方buff防御力） / 10
        //你的攻击力，和我的防御力之间的计算
        //attacker.damage.PoseIdx;
        int DefTmp = 0;
        AttackDes atk = attacker.CurrentDamage;
        int WeaponDamage = attacker.CalcDamage();
        int PoseDamage = MenuResLoader.Instance.FindOpt(atk.PoseIdx, 3).second[0].flag[6];
        int BuffDamage = attacker.Attr.CalcBuffDamage();
        int realDamage = Mathf.FloorToInt((((WeaponDamage + BuffDamage) * PoseDamage) / 100.0f - (DefTmp)) / 10.0f);
        return realDamage;
    }

    public void OnDamage(MeteorUnit attacker)
    {
        int realDamage = 8 * CalcDamage(attacker);
        //非铁箱子， 木箱子，酒坛， 桌子 椅子的受击处理
        if (MethodOnAttack != null)
            MethodOnAttack.Invoke(GameBattleEx.Instance.Script, new object[] { InstanceId, attacker.InstanceId, realDamage });
        else if (OnAttackCallBack != null)
        {
            OnAttackCallBack.Invoke(InstanceId, Index, realDamage);
        }
    }

    void SetCollision(bool en)
    {
        Collider[] co = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < co.Length; i++)
        {
            if (en)
                co[i].isTrigger = false;
            else
            {
                MeshCollider mc = co[i] as MeshCollider;
                if (mc != null)
                {
                    if (OnIdle != null)
                        mc.enabled = false;
                    else
                    {
                        mc.convex = true;
                        mc.inflateMesh = true;
                        mc.skinWidth = 0.2f;
                        mc.isTrigger = true;
                        mc.enabled = true;
                    }
                }
                else
                {
                    if (OnIdle != null)
                        co[i].enabled = false;
                    else
                    {
                        co[i].isTrigger = true;
                        co[i].enabled = true;
                    }
                }
            }
        }
    }

    public bool HasCollision()
    {
        return collisions.Count != 0;
    }

    public List<Collider> GetCollsion()
    {
        return collisions;
    }

    public int GetSceneItem(string feature)
    {
        if (feature == "pose")
        {
            if (player != null)
                return player.GetPose();
            return -1;
        }
        else if (feature == "state")
        {
            if (player != null)
                return player.GetStatus();
        }
        else if (feature == "index")
        {
            return InstanceId;
        }
        return -1;
    }

    bool asDrop = false;
    public void SetAsDrop()
    {
        asDrop = true;
    }

    public void SetAutoDestroy(float t)
    {
        Invoke("AutoDestroy", t);
    }

    public void AutoDestroy()
    {
        for (int i = 0; i < MeteorManager.Instance.SceneItems.Count; i++)
        {
            if (MeteorManager.Instance.SceneItems[i] != this 
                && MeteorManager.Instance.SceneItems[i].ItemInfo != null 
                && MeteorManager.Instance.SceneItems[i].ItemInfo.IsFlag())
            {
                MeteorManager.Instance.SceneItems[i].OnRefresh();
                break;
            }
        }
        MeteorManager.Instance.OnDestroySceneItem(this);
        GameObject.Destroy(gameObject);
    }

    int Index;
    public void OnAttack(System.Func<int, int, int, int> act, System.Action<int, int> idle)
    {
        OnAttackCallBack = act;
        OnIdle = idle;
        GameBattleEx.Instance.RemoveCollision(this);
        RefreshCollision();
        GameBattleEx.Instance.RegisterCollision(this);
        Index = int.Parse(name.Substring(name.Length - 2));
        WsGlobal.SetObjectLayer(gameObject, LayerMask.NameToLayer("Trigger"));
        //LayerMask.
    }
}