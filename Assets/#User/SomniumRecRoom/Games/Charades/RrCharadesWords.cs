namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Charades
{
    /// <summary>
    /// The word cards. Rec Room's list lived on its servers and is not in the bundles (rr_party.md §9), so this is
    /// our own family-friendly list in the same spirit: things that are fun to sculpt in 3D, with a handful of
    /// Rec Room's own things (paintball, goblins, the Rec Center) mixed in. Every client has the same list, so the
    /// instance state only carries an index; only the performer's client shows the word.
    /// </summary>
    public static class RrCharadesWords
    {
        public static readonly string[] All =
        {
            // animals
            "Dog", "Cat", "Fish", "Snake", "Spider", "Elephant", "Giraffe", "Penguin", "Octopus", "Shark",
            "Dinosaur", "Turtle", "Snail", "Butterfly", "Bee", "Owl", "Duck", "Frog", "Rabbit", "Horse",
            "Pig", "Cow", "Chicken", "Crab", "Whale", "Dolphin", "Kangaroo", "Monkey", "Lion", "Bat",
            "Worm", "Jellyfish", "Flamingo", "Unicorn", "Dragon", "Squid", "Hedgehog", "Crocodile", "Camel", "Mouse",
            // food
            "Pizza", "Hot Dog", "Hamburger", "Banana", "Apple", "Ice Cream", "Donut", "Cupcake", "Birthday Cake", "Popcorn",
            "Taco", "Sushi", "Spaghetti", "Pancakes", "Carrot", "Pineapple", "Watermelon", "Cheese", "Pretzel", "Cookie",
            "Candy Cane", "Lollipop", "French Fries", "Egg", "Sandwich", "Corn", "Mushroom", "Grapes", "Cherry", "Root Beer",
            // things
            "Chair", "Table", "Lamp", "Umbrella", "Glasses", "Hat", "Shoe", "Sock", "Key", "Clock",
            "Phone", "Television", "Computer", "Guitar", "Piano", "Drum", "Trumpet", "Microphone", "Headphones", "Camera",
            "Book", "Pencil", "Scissors", "Envelope", "Balloon", "Kite", "Candle", "Light Bulb", "Toothbrush", "Bathtub",
            "Bed", "Door", "Window", "Ladder", "Bucket", "Hammer", "Saw", "Anchor", "Crown", "Ring",
            "Treasure Chest", "Trophy", "Magnet", "Backpack", "Tent", "Sword", "Shield", "Bow and Arrow", "Magic Wand", "Mirror",
            "Teapot", "Fork", "Spoon", "Frying Pan", "Toaster", "Refrigerator", "Washing Machine", "Vacuum", "Broom", "Paint Brush",
            // vehicles and places
            "Car", "Bicycle", "Skateboard", "Train", "Airplane", "Helicopter", "Rocket", "Boat", "Submarine", "Bus",
            "Tractor", "Fire Truck", "Hot Air Balloon", "Roller Coaster", "Ferris Wheel", "Castle", "Lighthouse", "Igloo", "Pyramid", "Bridge",
            "House", "Tree House", "Volcano", "Island", "Mountain", "Waterfall", "Desert", "Cave", "Beach", "Rainbow",
            // nature and space
            "Sun", "Moon", "Star", "Planet", "Comet", "Cloud", "Lightning", "Snowman", "Snowflake", "Tornado",
            "Flower", "Cactus", "Palm Tree", "Leaf", "Tree", "Mushroom House", "Rain", "Wave", "Fire", "Alien",
            "UFO", "Astronaut", "Satellite", "Black Hole", "Telescope",
            // people and actions
            "Pirate", "Ninja", "Robot", "Ghost", "Vampire", "Mummy", "Zombie", "Wizard", "Knight", "Princess",
            "Clown", "Chef", "Doctor", "Firefighter", "Cowboy", "Mermaid", "Superhero", "Scarecrow", "Skeleton", "Viking",
            "Juggling", "Fishing", "Dancing", "Swimming", "Sleeping", "Painting", "Surfing", "Skiing", "Bowling", "Karate",
            // sports and games (Rec Room's own among them)
            "Basketball", "Soccer Ball", "Baseball Bat", "Tennis Racket", "Golf", "Boxing Glove", "Dart Board", "Chess", "Dice", "Yo-Yo",
            "Paintball", "Dodgeball", "Laser Tag", "Disc Golf", "Paddleball", "Goblin", "Rec Center", "Maker Pen", "Dorm Room", "Jumbotron",
            "Coach", "Quest", "Crossbow", "Treasure Map", "Pirate Ship", "Skull", "Cauldron", "Potion", "Jack-o-Lantern", "Haunted House",
            // harder ones
            "Time Machine", "Sandcastle", "Traffic Light", "Mailbox", "Parachute", "Spider Web", "Wind Turbine", "Windmill", "Dream Catcher", "Birdhouse",
            "Hourglass", "Compass", "Headphones", "Slide", "Swing", "Trampoline", "Merry-go-round", "Carousel", "Gumball Machine", "Jukebox",
            "Tuba", "Harp", "Violin", "Accordion", "Record Player", "Robot Dog", "Rubber Duck", "Teddy Bear", "Piggy Bank", "Party Hat"
        };

        /// <summary>The word for an index (wraps; never null).</summary>
        public static string Get(int index)
        {
            if (All.Length == 0)
            {
                return "?";
            }

            int i = index % All.Length;
            return All[i < 0 ? i + All.Length : i];
        }
    }
}
