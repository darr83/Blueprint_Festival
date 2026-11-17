THE ARTISTS' MUSIC

Each artist has a folder here with their name on it. Put that artist's songs in their folder.

HOW TO NAME THE FILES

    01 Song Title.mp3
    02 Another Song.mp3
    03 The Big Finish.mp3

  - Start with a number. Songs are played in number order, then round again until the set ends.
  - Then the song's title. This is what the game shows while the song plays.
  - .mp3, .ogg and .wav all work.
  - 2, 3 or 4 songs each is plenty. More is fine.

An artist with no songs yet still plays their set, in silence.

After adding files, open the game in Godot once so it can bring them in, then run it.

THE BEAT

The crowd dances in time to each song. The first time the game plays with a new song it works
out the beat and saves it beside the song, as "01 Song Title.mp3.beat". You can leave these
alone. If the crowd is ever out of time with a song, delete its .beat file to have it worked out
again, or change the numbers in it.

THE ARTISTS

  Marnie Thrupp           Solo guitarist                    Country     1 stars   320 fans
  Odell Ashby             Solo guitarist                    Pop         2 stars   4.2k fans
  Wren Hallows            Solo guitarist                    Rock        2 stars   6.8k fans
  Tobias Pell             Solo guitarist                    Country     3 stars   38k fans
  Salt and Bramble        Duo                               Pop         1 stars   540 fans
  Paper Magpies           Duo                               Pop         2 stars   5.1k fans
  Ida and Corin           Duo                               Country     2 stars   2.9k fans
  Crooked Lantern         Duo                               Hip hop     3 stars   52k fans
  Tin Foxgloves           Duo                               Rock        3 stars   27k fans
  The Muddy Gateposts     Four-piece band with a fiddle     Country     1 stars   780 fans
  Hollow Skylarks         Four-piece band with a fiddle     Country     2 stars   7.4k fans
  The Wandering Wellies   Four-piece band with a fiddle     Country     3 stars   44k fans
  The Copper Turnips      Five-piece band                   Rock        3 stars   61k fans
  Bramble Hammer          Five-piece band                   Metal       4 stars   240k fans
  The Velvet Otters       Five-piece band                   Pop         5 stars   2.6m fans
  Little Ferrets          Five-piece band, headline act     Rock        2 stars   8.9k fans
  Tin Thunder             Five-piece band, headline act     Metal       3 stars   33k fans
  Hedgerow Howl           Five-piece band, headline act     Metal       4 stars   310k fans
  The Lantern Hearts      Five-piece band, headline act     Pop         5 stars   1.9m fans
  DJ Haybale              DJ                                Electronic  2 stars   3.6k fans
  DJ Weathervane          DJ                                Hip hop     4 stars   180k fans
  DJ Tractor              DJ, headline act                  Electronic  2 stars   9.5k fans
  DJ Magpie               DJ, headline act                  Hip hop     3 stars   47k fans
  DJ Foxglove             DJ, headline act                  Electronic  4 stars   420k fans
  DJ Otter                DJ, headline act                  Electronic  5 stars   3.1m fans

  Puddle Club               Five-piece band                   Indie       2 stars   6.2k fans
  The Hayloft Assembly      Five-piece band                   Indie       3 stars   41k fans
  Cartwheel Season          Three-piece band                  Indie       4 stars   290k fans
  Nettle Rash               Three-piece band                  Punk        1 stars   610 fans
  The Rusty Kettles         Five-piece band, headline act     Punk        2 stars   7.8k fans
  Scarecrow Riot            Five-piece band, headline act     Punk        4 stars   210k fans
  Ivy and Thistle           Duo                               Folk        1 stars   450 fans
  The Millpond Fiddlers     Four-piece band with a fiddle     Folk        2 stars   3.9k fans
  The Barleycorn Circle     Four-piece band with a fiddle     Folk        3 stars   29k fans
  The Old Oak Ramblers      Four-piece band with a fiddle     Folk        4 stars   160k fans
  Sunny Allotment           Five-piece band                   Reggae      2 stars   5.5k fans
  The Orchard Sound System  Five-piece band                   Reggae      3 stars   36k fans
  Roots of the Willow       Five-piece band                   Reggae      4 stars   250k fans
  Della Marsh               Solo guitarist                    Soul        2 stars   8.1k fans
  The Honeysuckle Revue     Five-piece band                   Soul        3 stars   58k fans
  The Golden Sparrows       Five-piece band, headline act     Soul        5 stars   2.2m fans
  Glitterbarn               Five-piece band                   Disco       2 stars   9.1k fans
  The Mirrorball Mice       Five-piece band, headline act     Disco       3 stars   33k fans
  Starling Disco Club       Five-piece band, headline act     Disco       5 stars   2.8m fans
  Gravel Lane               Three-piece band                  Rock        1 stars   870 fans
  Iron Pitchfork            Five-piece band                   Metal       2 stars   6.6k fans
  Sugar Beet Social         Five-piece band                   Pop         2 stars   4.4k fans
  Cider Gate                Three-piece band                  Country     2 stars   3.1k fans
  Harvest Crew              Duo                               Hip hop     2 stars   6.9k fans
  DJ Pylon                  DJ                                Electronic  3 stars   51k fans

THE ENTERTAINERS

The guitarist, pianist, drummer and DJ you can hire to play in the camping and food fields have
their own folders inside "Entertainers". See the README there.

SING-ALONG CHORUSES

The crowd sings along in a song's choruses once they are marked and the song's crowd is made.
On the main menu press "Mark the choruses", pick a song, press Play, and tap C as each chorus
starts and again as it ends. The marks are saved beside the song as "<song file>.chorus" (for
example "01 Long Way Home.mp3.chorus"); delete that file to take the sing-along away.

The crowd itself is made from the song's own singing by tools/make_crowd_singing.py (or ask
Claude to make it): it pulls the voice out of the song and turns it into a field of people
singing, saved beside the song as "01 Long Way Home.crowd.ogg". The game plays it quietly under
the song in the marked choruses, louder the more people sing. Moving choruses later needs
nothing made again. A song's singing on its own, saved as "01 Long Way Home.vocals.mp3", is
used instead when there is no crowd file.

When a song has its crowd made, now and then (about one chorus in seven) the artist and the band
drop out for five to ten seconds and only the crowd sings, then they come back in, in time.

Every artist is made up. None is a real act.
