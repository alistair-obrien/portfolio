public static class SlotIds
{
    public static class Organic
    {
        public static readonly AnatomySlotId Head = new("character.organic.head");
        public static readonly AnatomySlotId Torso = new("character.organic.torso");
        public static readonly AnatomySlotId LeftArm = new("character.organic.left_arm");
        public static readonly AnatomySlotId RightArm = new("character.organic.right_arm");
        public static readonly AnatomySlotId LeftLeg = new("character.organic.left_leg");
        public static readonly AnatomySlotId RightLeg = new("character.organic.right_leg");
    }

    public static class Loadout
    {
        public static readonly LoadoutSlotId HeldItem = new("character.loadout.heldItem");
        public static readonly LoadoutSlotId Inventory = new("character.loadout.inventory");
        public static readonly LoadoutSlotId PrimaryWeapon = new("character.loadout.primaryWeapon");
        public static readonly LoadoutSlotId Armor = new("character.loadout.armor");
    }
    public static class Style
    {
        public static readonly StyleSlotId Head = new("character.style.head");
        public static readonly StyleSlotId Face = new("character.style.face");
        public static readonly StyleSlotId TorsoInner = new("character.style.torso_inner");
        public static readonly StyleSlotId TorsoOuter = new("character.style.torso_outer");
        public static readonly StyleSlotId Hands = new("character.style.hands");
        public static readonly StyleSlotId Legs = new("character.style.legs");
        public static readonly StyleSlotId Feet = new("character.style.feet");
        public static readonly StyleSlotId Accessory = new("character.style.accessory"); //HACK
        public static readonly StyleSlotId Accessory1 = new("character.style.accessory_1");
        public static readonly StyleSlotId Accessory2 = new("character.style.accessory_2");
        public static readonly StyleSlotId Accessory3 = new("character.style.accessory_3");
        public static readonly StyleSlotId Accessory4 = new("character.style.accessory_4");
        public static readonly StyleSlotId Accessory5 = new("character.style.accessory_5");
    }
    public static class Cybernetic
    {
        public static readonly CyberneticSlotId Head = new("character.cyber.head");
        public static readonly CyberneticSlotId Torso = new("character.cyber.torso");
        public static readonly CyberneticSlotId LeftArm = new("character.cyber.left_arm");
        public static readonly CyberneticSlotId RightArm = new("character.cyber.right_arm");
        public static readonly CyberneticSlotId Legs = new("character.cyber.legs");
        public static readonly CyberneticSlotId Internal1 = new("character.cyber.internal_1");
        public static readonly CyberneticSlotId Internal2 = new("character.cyber.internal_2");
        public static readonly CyberneticSlotId Internal3 = new("character.cyber.internal_3");
        public static readonly CyberneticSlotId Internal4 = new("character.cyber.internal_4");
        public static readonly CyberneticSlotId Internal5 = new("character.cyber.internal_5");
    }
}