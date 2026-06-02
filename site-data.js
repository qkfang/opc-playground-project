/* Site content — edit this file to update projects and updates */

var PROJECTS = [
  {
    title: "Rover-1: Indoor Navigation",
    description: "Differential-drive testbed implementing simultaneous localization and mapping (SLAM) for reliable indoor autonomous navigation over repeatable routes.",
    tags: ["Navigation", "SLAM", "ROS 2"],
    status: "active",
    links: [
      { label: "GitHub", href: "https://github.com/qkfang/opc-project-1" },
      { label: "Docs", href: "#" }
    ]
  },
  {
    title: "Arm-2: Tabletop Manipulation",
    description: "6-DoF robotic arm with perception-driven grasping using depth cameras and a parallel gripper. Achieves 85 % success on household-object pick-and-place.",
    tags: ["Manipulation", "Perception", "MoveIt"],
    status: "in-progress",
    links: [
      { label: "URDF", href: "#" },
      { label: "ROS nodes", href: "#" }
    ]
  },
  {
    title: "Drone-3: Waypoint Navigation",
    description: "Vision-aided autonomous flight stack for outdoor waypoint navigation using PX4 autopilot, GPS, and optical-flow hover stabilization.",
    tags: ["Aerial", "PX4", "Computer Vision"],
    status: "experimental",
    links: [
      { label: "Flight logs", href: "#" },
      { label: "Build guide", href: "#" }
    ]
  },
  {
    title: "Telemetry Dashboard",
    description: "Lightweight browser-based telemetry viewer for real-time monitoring of robot state, sensor readings, and mission progress — no server required.",
    tags: ["Web", "Telemetry", "Visualization"],
    status: "active",
    links: [
      { label: "Live demo", href: "./index.html" }
    ]
  }
];

var UPDATES = [
  {
    date: "2026-05-30",
    title: "Multi-page site launched",
    body: "Home, Projects, Updates, and Contact pages are live on GitHub Pages. Navigation and responsive layout verified on desktop and mobile."
  },
  {
    date: "2026-05-28",
    title: "Navigation baseline — Rover-1",
    body: "Configured mapping + localization pipeline; started collecting repeatable hallway datasets. Average path deviation under 8 cm."
  },
  {
    date: "2026-05-20",
    title: "Hardware bring-up",
    body: "Motor drivers tuned on Rover-1; safety E-stop and current limits verified on the bench. Ready for first autonomous runs."
  },
  {
    date: "2026-05-10",
    title: "Arm-2 grasping milestone",
    body: "Achieved 85 % grasp success rate on household objects using depth-camera-guided planning with MoveIt 2 and the parallel gripper."
  }
];
