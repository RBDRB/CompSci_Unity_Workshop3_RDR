# Prey Predator

Add this folder in Unity Hub. In Unity, open `Assets/Scenes/SampleScene.unity` and press **Play**.

The scene includes a ground plane with a material applied, a light, and a free camera. The models are in **Assets/Models**, and the ground material is in **Assets/Materials**. The wolf's animation clips are included in **Wolf.fbx**. You will create the prefabs, animation controllers, and animal behaviors.

Press **F2** to open the population graph. It stays empty until it receives data from the simulation. Call `plot.RecordPopulation(sheep, wolves, grass)` in your script whenever you record the population counts.

## Speeding up the simulation

Add the **Simulation Speed** component to the GameObject that runs your simulation. In the **Game** view, use the first four keys on the number row or the corresponding keys on the numeric keypad. **1 → ×1**, **2 → ×2**, **3 → ×4**, **4 → ×8**. The HUD displays the current simulation speed, and the camera moves at its usual speed.

## Model credits

The source files credit Grass Tuft to Vice Cooper, Heart to Google, Fork to Poly by Google, and Duck to Alexander Brodin.

## Fonctionnement

- **`Simulation`** fait apparaître un troupeau et une meute éloignés, fait naître les petits et
  envoie chaque seconde les populations au graphe F2. **`GrassField`** fait repousser l'herbe.
- Chaque animal est une **machine à états**. À chaque image, il **perçoit** (`OverlapSphere`),
  **décide** (`UpdateState`), **agit** (`Act`), puis s'écarte de ses voisins de 0.5 -> 1 quand effectué par les deux.
- **Mouton** : erre en troupeau, mange l'herbe quand il a faim, se reproduit quand il est rassasié,
  fuit les loups en priorité. Il meurt de faim ou tué, puis reste au sol comme carcasse.
- **Loup** : erre en meute, chasse quand il a faim, tue, mange la carcasse bouchée par bouchée
  (une bouchée par cycle de l'animation `Eating`), se reproduit quand il est rassasié, meurt de faim.

<img width="336" height="155" alt="image" src="https://github.com/user-attachments/assets/2d91e22b-fe71-4b88-9aaa-d24f123ba278" />

## Choix

- **Troupeau et meute (Boids)** : valeurs de cohésion / séparation / alignement avec les voisins
- **Fuite à deux seuils** (panique à 5 m, calme à 8 m) avec un **angle de panique** aléatoire pour qu'ils se dispersent dans tous les sens
- **Herbe réservée** par un seul mouton, avec abandon après 6 s sinon les moutons mouraient tous de faim
- **Reproduction par proposition** (l'un propose, l'autre accepte) : deux animaux se mettent
  d'accord quand ils ont la santé et la proximité suffisante et font un petit.
- **Pas de classe commune** mouton / loup : deux scripts lisibles pour deux espèces. Avec plus
  d'espèces, j'extrairais des composants partagés.

