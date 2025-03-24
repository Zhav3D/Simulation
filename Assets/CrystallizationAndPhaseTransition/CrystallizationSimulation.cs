using Random = UnityEngine.Random;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

namespace C1
{
    public class CrystallizationSimulation : MonoBehaviour
    {
        [System.Serializable]
        public class AtomType
        {
            public string name;
            public Color color = Color.white;
            public float mass = 1f;
            public float radius = 0.5f;
            public float bondingStrength = 1.0f; // How strongly this atom forms bonds
            public float spawnAmount = 50;
        }

        [System.Serializable]
        public class MolecularBond
        {
            public int typeIndexA;
            public int typeIndexB;
            public float optimalDistance; // Preferred distance between atoms
            public float bondStrength;    // Spring constant for bond
            public float breakThreshold;  // Temperature at which bond breaks
        }

        [Header("Simulation Settings")]
        [SerializeField, Range(0f, 5f)] private float simulationSpeed = 1.0f;
        [Range(0f, 400f)] public float temperature = 300.0f; // Temperature in Kelvin
        [Range(0f, 100f)] public float pressureMultiplier = 1.0f; // External pressure
        public Vector3 simulationBounds = new Vector3(10f, 10f, 10f);
        public float dampening = 0.95f; // Air resistance / friction
        public float interactionStrength = 1f; // Global multiplier for interaction forces
        public float minDistance = 0.5f; // Minimum distance to prevent extreme forces
        public float bounceForce = 0.8f; // Velocity preserved on collision with boundaries
        public float maxForce = 100f; // Maximum force to prevent instability
        public float maxVelocity = 20f; // Maximum velocity to prevent instability
        public float interactionRadius = 3f; // Maximum distance for atomic interactions

        [Header("Phase Transition Settings")]
        public float meltingPoint = 273.15f; // Temperature at which solid melts
        public float boilingPoint = 373.15f; // Temperature at which liquid boils
        public float crystalNucleationThreshold = 0.7f; // How closely atoms must be packed to start crystal formation
        public float coolingRate = 0.5f; // How fast temperature decreases per second
        public float heatingRate = 0.5f; // How fast temperature increases per second
        public bool autoCool = false; // Automatically cool the system
        public bool autoHeat = false; // Automatically heat the system

        public enum LatticeType { None, SimpleCubic, BodyCenteredCubic, FaceCenteredCubic, Hexagonal }
        [Header("Lattice Settings")]
        public LatticeType initialLattice = LatticeType.None;
        public float latticeSpacing = 1.2f; // Distance between lattice points

        [Header("Spatial Partitioning")]
        public float cellSize = 2.5f; // Size of each grid cell, should be >= interactionRadius/2
        public bool useGridPartitioning = true; // Toggle for spatial grid
        public bool useJobSystem = true; // Toggle for Jobs System

        [Header("Atom Types")]
        public List<AtomType> atomTypes = new List<AtomType>();

        [Header("Molecular Bonds")]
        public List<MolecularBond> molecularBonds = new List<MolecularBond>();

        [Header("Atom Generation")]
        public GameObject atomPrefab;

        [Header("Visualization")]
        public bool showBonds = true;
        public float bondThickness = 0.1f;
        public Material bondMaterial;
        public bool colorByPhase = true;
        public Color solidColor = new Color(0.2f, 0.2f, 0.8f, 1.0f);
        public Color liquidColor = new Color(0.2f, 0.6f, 0.8f, 0.8f);
        public Color gasColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);

        // Runtime variables
        private List<AtomParticle> atoms = new List<AtomParticle>();
        private Dictionary<(int, int), MolecularBond> bondLookup = new Dictionary<(int, int), MolecularBond>();
        private SpatialGrid spatialGrid;
        private List<BondRenderer> activeBonds = new List<BondRenderer>();
        private float lastTemperature; // For detecting temperature changes

        // Native arrays for Jobs System
        private NativeArray<AtomData> atomDataArray;
        private NativeArray<BondData> bondDataArray;
        private NativeArray<float3> forceArray;
        private NativeArray<AtomData> tempAtomDataArray;

        // Cached transform references for performance
        private Transform[] atomTransforms;

        // Phase state tracking
        private float averageKineticEnergy = 0f;
        private float totalBondEnergy = 0f;
        private int solidAtomCount = 0;
        private int liquidAtomCount = 0;
        private int gasAtomCount = 0;

        void Start()
        {
            // Build bond lookup table for quick access
            foreach (var bond in molecularBonds)
            {
                bondLookup[(bond.typeIndexA, bond.typeIndexB)] = bond;
                // Also add the reverse lookup for convenience
                bondLookup[(bond.typeIndexB, bond.typeIndexA)] = bond;
            }

            // Initialize spatial grid if enabled
            if (useGridPartitioning)
            {
                spatialGrid = new SpatialGrid(simulationBounds, cellSize);
            }

            // Initialize atoms - either in a lattice or randomly
            if (initialLattice != LatticeType.None)
            {
                SpawnAtomsInLattice();
            }
            else
            {
                SpawnAtomsRandomly();
            }

            lastTemperature = temperature; // Initialize temperature tracking

            // Initialize the Jobs System arrays if enabled
            if (useJobSystem)
            {
                InitializeJobsSystem();
            }
        }

        void OnDestroy()
        {
            // Clean up native arrays
            if (atomDataArray.IsCreated) atomDataArray.Dispose();
            if (tempAtomDataArray.IsCreated) tempAtomDataArray.Dispose();
            if (bondDataArray.IsCreated) bondDataArray.Dispose();
            if (forceArray.IsCreated) forceArray.Dispose();

            // Clean up bond renderers
            foreach (var bond in activeBonds)
            {
                if (bond != null && bond.gameObject != null)
                    Destroy(bond.gameObject);
            }
        }

        void Update()
        {
            Time.timeScale = simulationSpeed;

            // Handle automatic temperature changes
            if (autoCool)
            {
                temperature = Mathf.Max(0, temperature - coolingRate * Time.deltaTime);
            }
            else if (autoHeat)
            {
                temperature += heatingRate * Time.deltaTime;
            }

            // Detect significant temperature changes
            if (Mathf.Abs(temperature - lastTemperature) > 1.0f)
            {
                AdjustVelocitiesForTemperature();
                lastTemperature = temperature;
            }

            if (useJobSystem)
            {
                // Update data arrays from Unity objects
                UpdateAtomDataArray();

                // Run Jobs
                RunSimulationJobs(Time.deltaTime);

                // Apply results back to Unity objects
                ApplyJobResults();
            }
            else
            {
                // Use traditional update method
                UpdateAtoms(Time.deltaTime);
            }

            // Update bond visualizations and phase statistics
            if (showBonds)
            {
                UpdateBondRenderers();
            }

            CalculatePhaseStatistics();
            UpdateVisualsBasedOnPhase();
        }

        private void SpawnAtomsRandomly()
        {
            for (int typeIndex = 0; typeIndex < atomTypes.Count; typeIndex++)
            {
                var type = atomTypes[typeIndex];

                for (int i = 0; i < type.spawnAmount; i++)
                {
                    GameObject atomObj = Instantiate(atomPrefab, transform);

                    // Set random position within bounds
                    Vector3 randomPos = new Vector3(
                        Random.Range(-simulationBounds.x / 2, simulationBounds.x / 2),
                        Random.Range(-simulationBounds.y / 2, simulationBounds.y / 2),
                        Random.Range(-simulationBounds.z / 2, simulationBounds.z / 2)
                    );

                    atomObj.transform.position = randomPos;

                    // Add and configure atom component
                    AtomParticle atom = atomObj.AddComponent<AtomParticle>();
                    atom.typeIndex = typeIndex;
                    atom.mass = type.mass;
                    atom.radius = type.radius;
                    atom.bondingStrength = type.bondingStrength;

                    // Initialize a random velocity based on temperature
                    float velocityMagnitude = GetVelocityForTemperature(temperature, type.mass);
                    atom.velocity = Random.onUnitSphere * velocityMagnitude;

                    // Set visual properties
                    Renderer renderer = atomObj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = type.color;
                        atomObj.transform.localScale = Vector3.one * type.radius * 2;  // Diameter
                    }

                    // Give atom a name for debugging
                    atomObj.name = $"Atom_{type.name}_{i}";

                    // Add to our list
                    atoms.Add(atom);
                }
            }

            // Cache transform references
            atomTransforms = new Transform[atoms.Count];
            for (int i = 0; i < atoms.Count; i++)
            {
                atomTransforms[i] = atoms[i].transform;
            }
        }

        private void SpawnAtomsInLattice()
        {
            // Calculate how many atoms we can fit in the simulation bounds
            int atomsPerDimension = Mathf.FloorToInt(simulationBounds.x / latticeSpacing);
            Vector3 startPosition = -simulationBounds / 2 + Vector3.one * (latticeSpacing / 2);

            // For simplicity, only spawn one atom type in the lattice
            AtomType type = atomTypes[0];
            int typeIndex = 0;

            int atomCount = 0;

            switch (initialLattice)
            {
                case LatticeType.SimpleCubic:
                    for (int x = 0; x < atomsPerDimension; x++)
                    {
                        for (int y = 0; y < atomsPerDimension; y++)
                        {
                            for (int z = 0; z < atomsPerDimension; z++)
                            {
                                Vector3 position = startPosition + new Vector3(
                                    x * latticeSpacing,
                                    y * latticeSpacing,
                                    z * latticeSpacing
                                );

                                SpawnAtomAtPosition(position, type, typeIndex, atomCount);
                                atomCount++;
                            }
                        }
                    }
                    break;

                case LatticeType.BodyCenteredCubic:
                    for (int x = 0; x < atomsPerDimension; x++)
                    {
                        for (int y = 0; y < atomsPerDimension; y++)
                        {
                            for (int z = 0; z < atomsPerDimension; z++)
                            {
                                // Corner atom
                                Vector3 position = startPosition + new Vector3(
                                    x * latticeSpacing,
                                    y * latticeSpacing,
                                    z * latticeSpacing
                                );

                                SpawnAtomAtPosition(position, type, typeIndex, atomCount);
                                atomCount++;

                                // Center atom
                                if (x < atomsPerDimension - 1 && y < atomsPerDimension - 1 && z < atomsPerDimension - 1)
                                {
                                    Vector3 centerPosition = position + Vector3.one * (latticeSpacing / 2);
                                    SpawnAtomAtPosition(centerPosition, type, typeIndex, atomCount);
                                    atomCount++;
                                }
                            }
                        }
                    }
                    break;

                case LatticeType.FaceCenteredCubic:
                    for (int x = 0; x < atomsPerDimension; x++)
                    {
                        for (int y = 0; y < atomsPerDimension; y++)
                        {
                            for (int z = 0; z < atomsPerDimension; z++)
                            {
                                // Corner atom
                                Vector3 position = startPosition + new Vector3(
                                    x * latticeSpacing,
                                    y * latticeSpacing,
                                    z * latticeSpacing
                                );

                                SpawnAtomAtPosition(position, type, typeIndex, atomCount);
                                atomCount++;

                                if (x < atomsPerDimension - 1 && y < atomsPerDimension - 1)
                                {
                                    // Face center on XY plane
                                    Vector3 faceXY = position + new Vector3(latticeSpacing / 2, latticeSpacing / 2, 0);
                                    SpawnAtomAtPosition(faceXY, type, typeIndex, atomCount);
                                    atomCount++;
                                }

                                if (x < atomsPerDimension - 1 && z < atomsPerDimension - 1)
                                {
                                    // Face center on XZ plane
                                    Vector3 faceXZ = position + new Vector3(latticeSpacing / 2, 0, latticeSpacing / 2);
                                    SpawnAtomAtPosition(faceXZ, type, typeIndex, atomCount);
                                    atomCount++;
                                }

                                if (y < atomsPerDimension - 1 && z < atomsPerDimension - 1)
                                {
                                    // Face center on YZ plane
                                    Vector3 faceYZ = position + new Vector3(0, latticeSpacing / 2, latticeSpacing / 2);
                                    SpawnAtomAtPosition(faceYZ, type, typeIndex, atomCount);
                                    atomCount++;
                                }
                            }
                        }
                    }
                    break;

                case LatticeType.Hexagonal:
                    float hexHeight = latticeSpacing * Mathf.Sqrt(3) / 2;

                    for (int x = 0; x < atomsPerDimension; x++)
                    {
                        for (int y = 0; y < atomsPerDimension; y++)
                        {
                            for (int z = 0; z < atomsPerDimension; z++)
                            {
                                // Base position
                                Vector3 position = startPosition + new Vector3(
                                    x * latticeSpacing * 1.5f,
                                    y * hexHeight * 2,
                                    z * latticeSpacing * Mathf.Sqrt(3)
                                );

                                // First atom in unit cell
                                SpawnAtomAtPosition(position, type, typeIndex, atomCount);
                                atomCount++;

                                // Second atom offset in unit cell
                                if (x < atomsPerDimension - 1 && y < atomsPerDimension - 1)
                                {
                                    Vector3 offsetPos = position + new Vector3(
                                        latticeSpacing * 0.5f,
                                        hexHeight,
                                        latticeSpacing * Mathf.Sqrt(3) / 2
                                    );
                                    SpawnAtomAtPosition(offsetPos, type, typeIndex, atomCount);
                                    atomCount++;
                                }
                            }
                        }
                    }
                    break;
            }

            // Cache transform references
            atomTransforms = new Transform[atoms.Count];
            for (int i = 0; i < atoms.Count; i++)
            {
                atomTransforms[i] = atoms[i].transform;
            }

            Debug.Log($"Spawned {atomCount} atoms in {initialLattice} lattice");
        }

        private void SpawnAtomAtPosition(Vector3 position, AtomType type, int typeIndex, int index)
        {
            GameObject atomObj = Instantiate(atomPrefab, transform);
            atomObj.transform.position = position;

            // Add and configure atom component
            AtomParticle atom = atomObj.AddComponent<AtomParticle>();
            atom.typeIndex = typeIndex;
            atom.mass = type.mass;
            atom.radius = type.radius;
            atom.bondingStrength = type.bondingStrength;

            // Initialize a small random velocity based on temperature (reduced for lattice)
            float velocityMagnitude = GetVelocityForTemperature(temperature * 0.1f, type.mass);
            atom.velocity = Random.onUnitSphere * velocityMagnitude;

            // Set visual properties
            Renderer renderer = atomObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = type.color;
                atomObj.transform.localScale = Vector3.one * type.radius * 2;  // Diameter
            }

            // Give atom a name for debugging
            atomObj.name = $"Atom_{type.name}_{index}";

            // Add to our list
            atoms.Add(atom);
        }

        private float GetVelocityForTemperature(float temp, float atomMass)
        {
            // Basic kinetic theory: KE = (3/2)kT
            // v = sqrt(3kT/m)
            float boltzmannConstant = 1.380649e-23f; // J/K

            // Scale factor to make it work in our simulation scale
            float scaleFactor = 1e11f;

            return Mathf.Sqrt(3 * boltzmannConstant * temp * scaleFactor / atomMass);
        }

        private void AdjustVelocitiesForTemperature()
        {
            // Calculate current average kinetic energy
            float currentKE = 0;
            foreach (var atom in atoms)
            {
                currentKE += 0.5f * atom.mass * atom.velocity.sqrMagnitude;
            }
            currentKE /= atoms.Count;

            // Calculate target kinetic energy for new temperature
            float targetKE = 1.5f * temperature; // Simplified relation KE ~ T

            // Scale factor to adjust velocities
            float scaleFactor = Mathf.Sqrt(targetKE / currentKE);

            // Apply scaling to all atoms
            foreach (var atom in atoms)
            {
                atom.velocity *= scaleFactor;
            }
        }

        private void InitializeJobsSystem()
        {
            int atomCount = atoms.Count;
            int bondCount = molecularBonds.Count;

            // Create native arrays
            atomDataArray = new NativeArray<AtomData>(atomCount, Allocator.Persistent);
            tempAtomDataArray = new NativeArray<AtomData>(atomCount, Allocator.Persistent);
            bondDataArray = new NativeArray<BondData>(bondCount, Allocator.Persistent);
            forceArray = new NativeArray<float3>(atomCount, Allocator.Persistent);

            // Fill bond data array
            for (int i = 0; i < bondCount; i++)
            {
                var bond = molecularBonds[i];
                bondDataArray[i] = new BondData
                {
                    typeA = bond.typeIndexA,
                    typeB = bond.typeIndexB,
                    optimalDistance = bond.optimalDistance,
                    bondStrength = bond.bondStrength,
                    breakThreshold = bond.breakThreshold
                };
            }

            // Initial population of atom data
            UpdateAtomDataArray();
        }

        private void UpdateAtomDataArray()
        {
            for (int i = 0; i < atoms.Count; i++)
            {
                var atom = atoms[i];
                float3 position = atom.transform.position;

                atomDataArray[i] = new AtomData
                {
                    position = position,
                    velocity = atom.velocity,
                    typeIndex = atom.typeIndex,
                    mass = atom.mass,
                    radius = atom.radius,
                    bondingStrength = atom.bondingStrength,
                    bonded = atom.bonded,
                    bondCount = atom.bondCount
                };
            }
        }

        private void ApplyJobResults()
        {
            for (int i = 0; i < atoms.Count; i++)
            {
                var data = atomDataArray[i];
                atoms[i].velocity = data.velocity;
                atoms[i].bonded = data.bonded;
                atoms[i].bondCount = data.bondCount;
                atomTransforms[i].position = data.position;
            }
        }

        private void RunSimulationJobs(float deltaTime)
        {
            // Calculate forces
            var forceJob = new AtomForceJob
            {
                atoms = atomDataArray,
                bonds = bondDataArray,
                bondCount = molecularBonds.Count,
                temperature = temperature,
                interactionStrength = interactionStrength,
                minDistance = minDistance,
                maxForce = maxForce,
                interactionRadius = interactionRadius,
                forces = forceArray
            };

            // Update positions
            var updateJob = new AtomUpdateJob
            {
                forces = forceArray,
                deltaTime = deltaTime,
                dampening = dampening,
                halfBounds = simulationBounds * 0.5f,
                bounceForce = bounceForce,
                maxVelocity = maxVelocity,
                pressureForce = pressureMultiplier,
                atoms = atomDataArray
            };

            // Schedule and complete force and update jobs
            var forceHandle = forceJob.Schedule(atoms.Count, 64);
            var updateHandle = updateJob.Schedule(atoms.Count, 64, forceHandle);
            updateHandle.Complete();

            // Copy initial data to temp array
            tempAtomDataArray.CopyFrom(atomDataArray);

            // Track which array has the most recent data
            bool finalDataInMainArray = true;

            // Run the collision job for a few iterations to resolve penetrations
            int collisionIterations = 3; // More iterations = more stable but slower

            // Run collision iterations with double buffering
            for (int i = 0; i < collisionIterations; i++)
            {
                var collisionJob = new AtomCollisionJob
                {
                    inputAtoms = finalDataInMainArray ? atomDataArray : tempAtomDataArray,
                    outputAtoms = finalDataInMainArray ? tempAtomDataArray : atomDataArray,
                    minDistance = minDistance,
                    elasticity = 0.5f,
                    temperature = temperature
                };

                // Complete the job
                collisionJob.Schedule(atoms.Count, 64).Complete();

                // Toggle which array has the latest data
                finalDataInMainArray = !finalDataInMainArray;
            }

            // If final data is in temp array, copy back to main array
            if (!finalDataInMainArray)
            {
                atomDataArray.CopyFrom(tempAtomDataArray);
            }
        }

        private void UpdateAtoms(float deltaTime)
        {
            // Update spatial grid if enabled
            if (useGridPartitioning)
            {
                spatialGrid.UpdateGrid(atoms);
            }

            // Calculate atomic forces
            foreach (var atomA in atoms)
            {
                Vector3 totalForce = Vector3.zero;

                // Get atoms to check against (all or just nearby)
                List<AtomParticle> atomsToCheck;
                if (useGridPartitioning)
                {
                    atomsToCheck = spatialGrid.GetNearbyParticles(atomA.transform.position, interactionRadius);
                }
                else
                {
                    atomsToCheck = atoms;
                }

                // Reset bond status
                atomA.bonded = false;
                atomA.bondCount = 0;

                // Check interaction with relevant atoms
                foreach (var atomB in atomsToCheck)
                {
                    if (atomA == atomB) continue;

                    // Calculate direction and distance
                    Vector3 direction = atomB.transform.position - atomA.transform.position;
                    float distance = direction.magnitude;

                    // Skip if too far away
                    if (distance > interactionRadius) continue;

                    // Prevent division by zero or extreme forces
                    if (distance < minDistance) distance = minDistance;

                    // Check if there's a specific bond between these atom types
                    if (bondLookup.TryGetValue((atomA.typeIndex, atomB.typeIndex), out MolecularBond bond))
                    {
                        // Skip bond interactions if temperature is above break threshold
                        if (temperature > bond.breakThreshold)
                            continue;

                        // Calculate spring force for bond (Hooke's law: F = -k * (x - x0))
                        float displacement = distance - bond.optimalDistance;
                        float forceMagnitude = -bond.bondStrength * displacement;

                        // Adjust force based on the bonding strength of both atoms
                        forceMagnitude *= atomA.bondingStrength * atomB.bondingStrength;

                        // Apply force in the right direction
                        totalForce += direction.normalized * forceMagnitude;

                        // Mark atom as bonded if it's close to the optimal distance
                        if (Mathf.Abs(displacement) < bond.optimalDistance * 0.2f)
                        {
                            atomA.bonded = true;
                            atomA.bondCount++;
                        }
                    }
                    else
                    {
                        // Default atomic interaction (Lennard-Jones potential approximation)
                        float combinedRadius = atomA.radius + atomB.radius;
                        float optimalDistance = combinedRadius * 1.1f; // Slightly more than touching

                        // Calculate force using Lennard-Jones 6-12 potential
                        float sigma = optimalDistance / 1.122f; // Distance where potential is zero
                        float sigmaPow6 = Mathf.Pow(sigma / distance, 6);
                        float sigmaPow12 = sigmaPow6 * sigmaPow6;

                        // Simplified L-J force
                        float forceMagnitude = 24 * interactionStrength *
                                             (2 * sigmaPow12 / distance - sigmaPow6 / distance);

                        // Temperature effect - higher temps reduce attractive forces
                        float tempFactor = Mathf.Clamp01(1.0f - temperature / 1000.0f);
                        if (forceMagnitude < 0) // Attractive force
                        {
                            forceMagnitude *= tempFactor;
                        }

                        // Cap the force magnitude
                        forceMagnitude = Mathf.Clamp(forceMagnitude, -maxForce, maxForce);

                        // Apply force
                        totalForce += direction.normalized * forceMagnitude;
                    }
                }

                // Apply external pressure (towards center)
                Vector3 centerDirection = -atomA.transform.position.normalized;
                float distanceFromCenter = atomA.transform.position.magnitude;
                float pressureForce = pressureMultiplier * distanceFromCenter * 0.1f;
                totalForce += centerDirection * pressureForce;

                // Apply force as acceleration (F = ma, so a = F/m)
                Vector3 acceleration = totalForce / atomA.mass;

                // Apply temperature-based random motion
                if (temperature > 0)
                {
                    float randomForce = Mathf.Sqrt(temperature) * 0.01f;
                    acceleration += Random.insideUnitSphere * randomForce;
                }

                // Cap acceleration to prevent numerical instability
                float maxAccel = 50f;
                if (acceleration.sqrMagnitude > maxAccel * maxAccel)
                {
                    acceleration = acceleration.normalized * maxAccel;
                }

                atomA.velocity += acceleration * deltaTime;

                // Cap velocity to prevent numerical instability
                if (atomA.velocity.sqrMagnitude > maxVelocity * maxVelocity)
                {
                    atomA.velocity = atomA.velocity.normalized * maxVelocity;
                }
            }

            // Update positions
            foreach (var atom in atoms)
            {
                // Apply dampening
                atom.velocity *= dampening;

                // Update position
                atom.transform.position += atom.velocity * deltaTime;
            }

            // Resolve collisions between atoms
            for (int i = 0; i < atoms.Count; i++)
            {
                var atomA = atoms[i];
                Vector3 posA = atomA.transform.position;
                float radiusA = atomA.radius;

                // Get atoms to check against (all or just nearby)
                List<AtomParticle> atomsToCheck;
                if (useGridPartitioning)
                {
                    atomsToCheck = spatialGrid.GetNearbyParticles(posA, radiusA * 2f);
                }
                else
                {
                    atomsToCheck = atoms;
                }

                foreach (var atomB in atomsToCheck)
                {
                    if (atomA == atomB) continue;

                    Vector3 posB = atomB.transform.position;
                    float radiusB = atomB.radius;

                    // Calculate overlap
                    Vector3 direction = posB - posA;
                    float distance = direction.magnitude;
                    float minDist = radiusA + radiusB;

                    // If atoms are overlapping
                    if (distance < minDist && distance > 0.001f)
                    {
                        // Calculate penetration depth
                        float penetrationDepth = minDist - distance;
                        Vector3 normal = direction.normalized;

                        // Calculate separation based on inverse mass ratio
                        float totalMass = atomA.mass + atomB.mass;
                        float ratioA = atomB.mass / totalMass;
                        float ratioB = atomA.mass / totalMass;

                        // Move atoms apart
                        atomA.transform.position -= normal * penetrationDepth * ratioA;
                        atomB.transform.position += normal * penetrationDepth * ratioB;

                        // Apply collision response with temperature-dependent elasticity
                        float elasticity = 0.5f * Mathf.Clamp01(temperature / 500f);

                        // Calculate relative velocity
                        Vector3 relativeVelocity = atomB.velocity - atomA.velocity;

                        // Calculate impulse
                        float impulse = (-(1 + elasticity) * Vector3.Dot(relativeVelocity, normal)) /
                                       (1 / atomA.mass + 1 / atomB.mass);

                        // Higher impulse for higher temperature
                        impulse *= 1 + (temperature / 1000f);

                        // Apply impulse
                        atomA.velocity -= normal * (impulse / atomA.mass);
                        atomB.velocity += normal * (impulse / atomB.mass);
                    }
                }
            }

            // Handle boundary collisions
            foreach (var atom in atoms)
            {
                // Check boundaries and bounce if needed
                Vector3 position = atom.transform.position;
                Vector3 halfBounds = simulationBounds / 2;

                // X boundaries
                if (position.x < -halfBounds.x + atom.radius)
                {
                    position.x = -halfBounds.x + atom.radius;
                    atom.velocity.x = -atom.velocity.x * bounceForce;
                }
                else if (position.x > halfBounds.x - atom.radius)
                {
                    position.x = halfBounds.x - atom.radius;
                    atom.velocity.x = -atom.velocity.x * bounceForce;
                }

                // Y boundaries
                if (position.y < -halfBounds.y + atom.radius)
                {
                    position.y = -halfBounds.y + atom.radius;
                    atom.velocity.y = -atom.velocity.y * bounceForce;
                }
                else if (position.y > halfBounds.y - atom.radius)
                {
                    position.y = halfBounds.y - atom.radius;
                    atom.velocity.y = -atom.velocity.y * bounceForce;
                }

                // Z boundaries
                if (position.z < -halfBounds.z + atom.radius)
                {
                    position.z = -halfBounds.z + atom.radius;
                    atom.velocity.z = -atom.velocity.z * bounceForce;
                }
                else if (position.z > halfBounds.z - atom.radius)
                {
                    position.z = halfBounds.z - atom.radius;
                    atom.velocity.z = -atom.velocity.z * bounceForce;
                }

                // Apply corrected position
                atom.transform.position = position;
            }
        }

        private void UpdateBondRenderers()
        {
            // Clean up any existing bond renderers
            foreach (var bond in activeBonds)
            {
                if (bond != null && bond.gameObject != null)
                    Destroy(bond.gameObject);
            }

            activeBonds.Clear();

            // Skip bond rendering if temperature is high enough that bonds would break
            if (temperature > boilingPoint)
                return;

            // Dictionary to track which bonds we've already rendered
            HashSet<(int, int)> renderedBonds = new HashSet<(int, int)>();

            // Create new bond renderers
            for (int i = 0; i < atoms.Count; i++)
            {
                var atomA = atoms[i];

                // Only check nearby atoms
                List<AtomParticle> atomsToCheck;
                if (useGridPartitioning)
                {
                    atomsToCheck = spatialGrid.GetNearbyParticles(atomA.transform.position, interactionRadius);
                }
                else
                {
                    atomsToCheck = atoms;
                }

                foreach (var atomB in atomsToCheck)
                {
                    // Skip self and already rendered bonds
                    if (atomA == atomB || renderedBonds.Contains((i, atomB.GetInstanceID())) ||
                        renderedBonds.Contains((atomB.GetInstanceID(), i)))
                        continue;

                    // Check if there's a bond between these atom types
                    if (bondLookup.TryGetValue((atomA.typeIndex, atomB.typeIndex), out MolecularBond bond))
                    {
                        // Skip if temperature is above break threshold
                        if (temperature > bond.breakThreshold)
                            continue;

                        // Calculate distance
                        Vector3 direction = atomB.transform.position - atomA.transform.position;
                        float distance = direction.magnitude;

                        // Only render bonds that are close to their optimal distance
                        if (Mathf.Abs(distance - bond.optimalDistance) < bond.optimalDistance * 0.3f)
                        {
                            // Create a cylinder for the bond
                            GameObject bondObj = new GameObject($"Bond_{i}_{atomB.GetInstanceID()}");
                            bondObj.transform.SetParent(transform);

                            BondRenderer renderer = bondObj.AddComponent<BondRenderer>();
                            renderer.Initialize(atomA, atomB, bondMaterial, bondThickness);

                            activeBonds.Add(renderer);
                            renderedBonds.Add((i, atomB.GetInstanceID()));
                        }
                    }
                }
            }
        }

        private void CalculatePhaseStatistics()
        {
            // Reset counters
            solidAtomCount = 0;
            liquidAtomCount = 0;
            gasAtomCount = 0;
            averageKineticEnergy = 0;
            totalBondEnergy = 0;

            // Calculate average kinetic energy
            foreach (var atom in atoms)
            {
                float ke = 0.5f * atom.mass * atom.velocity.sqrMagnitude;
                averageKineticEnergy += ke;

                // Determine phase state based on bonds and velocity
                if (atom.bondCount >= 4)
                {
                    solidAtomCount++;
                }
                else if (atom.bondCount > 0 || atom.velocity.magnitude < GetVelocityForTemperature(boilingPoint, atom.mass))
                {
                    liquidAtomCount++;
                }
                else
                {
                    gasAtomCount++;
                }
            }

            if (atoms.Count > 0)
                averageKineticEnergy /= atoms.Count;
        }

        private void UpdateVisualsBasedOnPhase()
        {
            if (!colorByPhase)
                return;

            foreach (var atom in atoms)
            {
                Renderer renderer = atom.GetComponent<Renderer>();
                if (renderer == null)
                    continue;

                // Determine color based on phase
                Color phaseColor;
                if (atom.bondCount >= 4)
                {
                    phaseColor = solidColor;
                }
                else if (atom.bondCount > 0 || atom.velocity.magnitude < GetVelocityForTemperature(boilingPoint, atom.mass))
                {
                    phaseColor = liquidColor;
                }
                else
                {
                    phaseColor = gasColor;
                }

                // Blend phase color with atom type color
                Color typeColor = atomTypes[atom.typeIndex].color;
                renderer.material.color = Color.Lerp(typeColor, phaseColor, 0.6f);
            }
        }

        void OnGUI()
        {
            // Display simulation stats
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Temperature: {temperature:F1} K");
            GUILayout.Label($"Phase Distribution:");
            GUILayout.Label($"  Solid: {solidAtomCount} atoms ({(float)solidAtomCount / atoms.Count * 100:F1}%)");
            GUILayout.Label($"  Liquid: {liquidAtomCount} atoms ({(float)liquidAtomCount / atoms.Count * 100:F1}%)");
            GUILayout.Label($"  Gas: {gasAtomCount} atoms ({(float)gasAtomCount / atoms.Count * 100:F1}%)");
            GUILayout.Label($"Average KE: {averageKineticEnergy:F2}");
            GUILayout.Label($"Active Bonds: {activeBonds.Count}");
            GUILayout.EndArea();
        }

        void OnDrawGizmos()
        {
            // Draw the simulation bounds
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, simulationBounds);
        }
    }

    [System.Serializable]
    public struct AtomData
    {
        public float3 position;
        public float3 velocity;
        public int typeIndex;
        public float mass;
        public float radius;
        public float bondingStrength;
        public bool bonded;
        public int bondCount;
    }

    [System.Serializable]
    public struct BondData
    {
        public int typeA;
        public int typeB;
        public float optimalDistance;
        public float bondStrength;
        public float breakThreshold;
    }

    [BurstCompile]
    public struct AtomForceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AtomData> atoms;
        [ReadOnly] public NativeArray<BondData> bonds;
        [ReadOnly] public int bondCount;
        [ReadOnly] public float temperature;
        [ReadOnly] public float interactionStrength;
        [ReadOnly] public float minDistance;
        [ReadOnly] public float maxForce;
        [ReadOnly] public float interactionRadius;

        public NativeArray<float3> forces;

        public void Execute(int index)
        {
            AtomData atomA = atoms[index];
            float3 totalForce = float3.zero;
            int bondCounter = 0;
            bool isBonded = false;

            for (int i = 0; i < atoms.Length; i++)
            {
                if (i == index) continue;

                AtomData atomB = atoms[i];

                // Calculate direction and distance
                float3 direction = atomB.position - atomA.position;
                float distance = math.length(direction);

                // Skip if too far away (optimization)
                if (distance > interactionRadius) continue;

                // Prevent division by zero or extreme forces
                if (distance < minDistance) distance = minDistance;

                // Check if there's a specific bond between these types
                bool foundBond = false;

                for (int b = 0; b < bondCount; b++)
                {
                    BondData bond = bonds[b];
                    if ((bond.typeA == atomA.typeIndex && bond.typeB == atomB.typeIndex) ||
                        (bond.typeA == atomB.typeIndex && bond.typeB == atomA.typeIndex))
                    {
                        // Skip bond interactions if temperature is above break threshold
                        if (temperature > bond.breakThreshold)
                            continue;

                        foundBond = true;

                        // Calculate spring force for bond (Hooke's law: F = -k * (x - x0))
                        float displacement = distance - bond.optimalDistance;
                        float forceMagnitude = -bond.bondStrength * displacement;

                        // Adjust force based on the bonding strength of both atoms
                        forceMagnitude *= atomA.bondingStrength * atomB.bondingStrength;

                        // Apply force in the right direction
                        totalForce += math.normalizesafe(direction) * forceMagnitude;

                        // Mark atom as bonded if it's close to the optimal distance
                        if (math.abs(displacement) < bond.optimalDistance * 0.2f)
                        {
                            isBonded = true;
                            bondCounter++;
                        }

                        break;
                    }
                }

                if (!foundBond)
                {
                    // Default atomic interaction (Lennard-Jones potential approximation)
                    float combinedRadius = atomA.radius + atomB.radius;
                    float optimalDistance = combinedRadius * 1.1f; // Slightly more than touching

                    // Calculate force using Lennard-Jones 6-12 potential
                    float sigma = optimalDistance / 1.122f; // Distance where potential is zero
                    float sigmaPow6 = math.pow(sigma / distance, 6);
                    float sigmaPow12 = sigmaPow6 * sigmaPow6;

                    // Simplified L-J force
                    float forceMagnitude = 24 * interactionStrength *
                                         (2 * sigmaPow12 / distance - sigmaPow6 / distance);

                    // Temperature effect - higher temps reduce attractive forces
                    float tempFactor = math.clamp(1.0f - temperature / 1000.0f, 0, 1);
                    if (forceMagnitude < 0) // Attractive force
                    {
                        forceMagnitude *= tempFactor;
                    }

                    // Cap the force magnitude
                    forceMagnitude = math.clamp(forceMagnitude, -maxForce, maxForce);

                    // Apply force
                    totalForce += math.normalizesafe(direction) * forceMagnitude;
                }
            }

            // Store bonding information in the output
            AtomData modifiedAtom = atoms[index];
            modifiedAtom.bonded = isBonded;
            modifiedAtom.bondCount = bondCounter;

            forces[index] = totalForce;
        }
    }

    [BurstCompile]
    public struct AtomUpdateJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> forces;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float dampening;
        [ReadOnly] public float3 halfBounds;
        [ReadOnly] public float bounceForce;
        [ReadOnly] public float maxVelocity;
        [ReadOnly] public float pressureForce;

        public NativeArray<AtomData> atoms;

        public void Execute(int index)
        {
            AtomData atom = atoms[index];

            // Apply force as acceleration (F = ma, so a = F/m)
            float3 acceleration = forces[index] / atom.mass;

            // Apply external pressure (towards center)
            float3 centerDirection = -math.normalize(atom.position);
            float distanceFromCenter = math.length(atom.position);
            float pressureAmount = pressureForce * distanceFromCenter * 0.1f;
            acceleration += centerDirection * pressureAmount;

            // Cap acceleration to prevent numerical instability
            float maxAccel = 50f;
            if (math.lengthsq(acceleration) > maxAccel * maxAccel)
            {
                acceleration = math.normalizesafe(acceleration) * maxAccel;
            }

            // Update velocity
            atom.velocity += acceleration * deltaTime;

            // Apply dampening
            atom.velocity *= dampening;

            // Cap velocity to prevent numerical instability
            if (math.lengthsq(atom.velocity) > maxVelocity * maxVelocity)
            {
                atom.velocity = math.normalizesafe(atom.velocity) * maxVelocity;
            }

            // Update position
            atom.position += atom.velocity * deltaTime;

            // Check boundaries and bounce if needed
            float3 position = atom.position;

            // X boundaries
            if (position.x < -halfBounds.x + atom.radius)
            {
                position.x = -halfBounds.x + atom.radius;
                atom.velocity.x = -atom.velocity.x * bounceForce;
            }
            else if (position.x > halfBounds.x - atom.radius)
            {
                position.x = halfBounds.x - atom.radius;
                atom.velocity.x = -atom.velocity.x * bounceForce;
            }

            // Y boundaries
            if (position.y < -halfBounds.y + atom.radius)
            {
                position.y = -halfBounds.y + atom.radius;
                atom.velocity.y = -atom.velocity.y * bounceForce;
            }
            else if (position.y > halfBounds.y - atom.radius)
            {
                position.y = halfBounds.y - atom.radius;
                atom.velocity.y = -atom.velocity.y * bounceForce;
            }

            // Z boundaries
            if (position.z < -halfBounds.z + atom.radius)
            {
                position.z = -halfBounds.z + atom.radius;
                atom.velocity.z = -atom.velocity.z * bounceForce;
            }
            else if (position.z > halfBounds.z - atom.radius)
            {
                position.z = halfBounds.z - atom.radius;
                atom.velocity.z = -atom.velocity.z * bounceForce;
            }

            // Update atom with new values
            atom.position = position;
            atoms[index] = atom;
        }
    }

    [BurstCompile]
    public struct AtomCollisionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AtomData> inputAtoms;
        public NativeArray<AtomData> outputAtoms;
        [ReadOnly] public float minDistance;
        [ReadOnly] public float elasticity;
        [ReadOnly] public float temperature;

        public void Execute(int indexA)
        {
            // Start with the input data for our output
            AtomData atomA = inputAtoms[indexA];
            outputAtoms[indexA] = atomA;

            float3 posA = atomA.position;
            float radiusA = atomA.radius;

            for (int indexB = 0; indexB < inputAtoms.Length; indexB++)
            {
                if (indexA == indexB) continue;

                AtomData atomB = inputAtoms[indexB];
                float3 posB = atomB.position;
                float radiusB = atomB.radius;

                // Calculate overlap
                float3 direction = posB - posA;
                float distance = math.length(direction);
                float collisionDistance = radiusA + radiusB;

                // If atoms are overlapping
                if (distance < collisionDistance && distance > 0.001f)
                {
                    // Get our current output atom
                    AtomData outputAtom = outputAtoms[indexA];

                    // Calculate penetration depth
                    float penetrationDepth = collisionDistance - distance;
                    float3 normal = math.normalize(direction);

                    // Calculate separation based on inverse mass ratio
                    float totalMass = atomA.mass + atomB.mass;
                    float ratioA = atomB.mass / totalMass;

                    // Apply position correction
                    outputAtom.position -= normal * penetrationDepth * ratioA;

                    // Apply collision response with temperature-dependent elasticity
                    float temp_elasticity = elasticity * math.clamp(temperature / 500f, 0, 1);

                    float3 relativeVelocity = atomB.velocity - atomA.velocity;
                    float impulse = (-(1 + temp_elasticity) * math.dot(relativeVelocity, normal)) /
                                   (1 / atomA.mass + 1 / atomB.mass);

                    // Higher impulse for higher temperature
                    impulse *= 1 + (temperature / 1000f);

                    // Apply impulse to our atom only
                    outputAtom.velocity -= normal * (impulse / atomA.mass);

                    // Update the output atom
                    outputAtoms[indexA] = outputAtom;
                }
            }
        }
    }

    // Spatial partitioning grid for optimizing neighbor lookups
    public class SpatialGrid
    {
        private List<AtomParticle>[] cells;
        private Vector3 gridWorldSize;
        private Vector3 cellSize;
        private int gridSizeX, gridSizeY, gridSizeZ;

        public SpatialGrid(Vector3 worldSize, float cellSize)
        {
            this.gridWorldSize = worldSize;
            this.cellSize = new Vector3(cellSize, cellSize, cellSize);

            gridSizeX = Mathf.CeilToInt(worldSize.x / cellSize);
            gridSizeY = Mathf.CeilToInt(worldSize.y / cellSize);
            gridSizeZ = Mathf.CeilToInt(worldSize.z / cellSize);

            int totalCells = gridSizeX * gridSizeY * gridSizeZ;
            cells = new List<AtomParticle>[totalCells];

            for (int i = 0; i < totalCells; i++)
            {
                cells[i] = new List<AtomParticle>();
            }
        }

        public void UpdateGrid(List<AtomParticle> particles)
        {
            // Clear all cells
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Clear();
            }

            // Add particles to appropriate cells
            foreach (var particle in particles)
            {
                int cellIndex = GetCellIndex(particle.transform.position);
                if (cellIndex >= 0 && cellIndex < cells.Length)
                {
                    cells[cellIndex].Add(particle);
                }
            }
        }

        public List<AtomParticle> GetNearbyParticles(Vector3 position, float radius)
        {
            List<AtomParticle> nearbyParticles = new List<AtomParticle>();
            Vector3 halfWorldSize = gridWorldSize * 0.5f;

            // Calculate the cell indices for the specified radius
            Vector3Int minCellPos = WorldToCell(position - new Vector3(radius, radius, radius) + halfWorldSize);
            Vector3Int maxCellPos = WorldToCell(position + new Vector3(radius, radius, radius) + halfWorldSize);

            // Clamp to grid boundaries
            minCellPos.x = Mathf.Max(0, minCellPos.x);
            minCellPos.y = Mathf.Max(0, minCellPos.y);
            minCellPos.z = Mathf.Max(0, minCellPos.z);

            maxCellPos.x = Mathf.Min(gridSizeX - 1, maxCellPos.x);
            maxCellPos.y = Mathf.Min(gridSizeY - 1, maxCellPos.y);
            maxCellPos.z = Mathf.Min(gridSizeZ - 1, maxCellPos.z);

            // Iterate through all cells in the specified range
            for (int x = minCellPos.x; x <= maxCellPos.x; x++)
            {
                for (int y = minCellPos.y; y <= maxCellPos.y; y++)
                {
                    for (int z = minCellPos.z; z <= maxCellPos.z; z++)
                    {
                        int cellIndex = GetCellIndex(x, y, z);
                        if (cellIndex >= 0 && cellIndex < cells.Length)
                        {
                            nearbyParticles.AddRange(cells[cellIndex]);
                        }
                    }
                }
            }

            return nearbyParticles;
        }

        private Vector3Int WorldToCell(Vector3 worldPosition)
        {
            Vector3 normalizedPos = (worldPosition + gridWorldSize * 0.5f);
            normalizedPos.x /= cellSize.x;
            normalizedPos.y /= cellSize.y;
            normalizedPos.z /= cellSize.z;
            return new Vector3Int(
                Mathf.FloorToInt(normalizedPos.x),
                Mathf.FloorToInt(normalizedPos.y),
                Mathf.FloorToInt(normalizedPos.z)
            );
        }

        private int GetCellIndex(Vector3 worldPosition)
        {
            Vector3 halfWorldSize = gridWorldSize * 0.5f;
            Vector3 adjustedPos = worldPosition + halfWorldSize;

            // Check if position is within grid bounds
            if (adjustedPos.x < 0 || adjustedPos.x >= gridWorldSize.x ||
                adjustedPos.y < 0 || adjustedPos.y >= gridWorldSize.y ||
                adjustedPos.z < 0 || adjustedPos.z >= gridWorldSize.z)
            {
                return -1;
            }

            Vector3Int cellPos = WorldToCell(worldPosition);
            return GetCellIndex(cellPos.x, cellPos.y, cellPos.z);
        }

        private int GetCellIndex(int x, int y, int z)
        {
            return x + y * gridSizeX + z * gridSizeX * gridSizeY;
        }
    }
}