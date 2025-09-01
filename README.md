# BNPCTrack

Dalamud Plugin for analyzing BNPC behavior.

Tracks actor position as well as ability to path their position using the Ramer-Douglas-Peucker (RDP) algorithm, or identifying clusters and denoising of positions using DBSCAN algorithm.

It offers plot graphs to allow visualizing path direction, speed, simulated simplified RDP curve as well as identifying clusters.

This is part of research for Sapphire server emulator in order to reverse mob patrols into path points and other behavior.

## RDP Usage

Click Run RDP to process the current sampled positions.
Select a BNPC and let it run its path, avoid aggroing the mob.
The grey path indicates the raw sampled positions. The coloured path indicates the processed RDP, with arrow directions and colour for speed (not much useful for FFXIV).
Cyan points are start, red for finish, and yellow are intermediate points.

The epsilon may be adjusted if the simplified RDP path still has too many points. A range of 0.50~0.80 is sufficient for most cases.

## DBSCAN Usage

It is recommended to use a very, very low sampling rate for DBSCAN as it is sensitive to roaming/intermediate clustering points.
DBSCAN is currently slow as it is O(N^2).