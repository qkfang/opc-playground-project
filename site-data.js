window.siteContent = {
  projects: [
    {
      title: "Atlas Rover",
      description: "A differential-drive rover for indoor navigation experiments, repeatable map updates, and autonomy benchmarking.",
      tags: ["Navigation", "SLAM", "ROS 2"],
      status: "Field testing",
      links: [
        { label: "Repository", url: "https://github.com/qkfang/opc-project-1" },
        { label: "Run log", url: "#updates" }
      ]
    },
    {
      title: "Relay Arm",
      description: "A perception-guided manipulator pipeline for object handoff, bin sorting, and grasp validation on a compact workcell.",
      tags: ["Manipulation", "Vision", "Control"],
      status: "Prototype",
      links: [
        { label: "Design notes", url: "#home" },
        { label: "Latest update", url: "#updates" }
      ]
    },
    {
      title: "SkyScan Quad",
      description: "A lightweight aerial robotics stack for waypoint inspection, live telemetry, and camera-assisted localization.",
      tags: ["Aerial", "Telemetry", "PX4"],
      status: "Integration",
      links: [
        { label: "Mission brief", url: "#home" },
        { label: "Contact team", url: "#contact" }
      ]
    }
  ],
  updates: [
    {
      date: "2026-06-01",
      title: "Robotics project hub published",
      body: "Shipped the new GitHub Pages structure with dedicated Home, Projects, Updates, and Contact sections.",
      highlights: [
        "Consolidated the landing content into a fast single-page site",
        "Added editable project cards and a reverse-chronological update feed"
      ]
    },
    {
      date: "2026-05-29",
      title: "Mobile field notes added",
      body: "Validated the responsive layout for phones and tightened the content blocks for quick scanning during demos.",
      highlights: [
        "Improved section spacing for smaller viewports",
        "Prepared a no-backend contact workflow that opens the visitor's email client"
      ]
    }
  ]
};
