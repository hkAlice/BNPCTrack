# BNPCTrack

Dalamud Plugin for analyzing BNPC behavior. I'd keep this disabled unless in use.

Tracks actor position as well as ability to path their position using the Ramer-Douglas-Peucker (RDP) algorithm, or identifying clusters and denoising of positions using DBSCAN algorithm.

It offers plot graphs to allow visualizing path direction, speed, simulated simplified RDP curve as well as identifying clusters.

DBSCAN is currently slow as it is O(N^2).

This is part of research for Sapphire server emulator in order to reverse mob patrols into path points and other behavior.