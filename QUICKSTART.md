# Quick Start Guide

## Lead Registration Web Portal - Getting Started in 5 Minutes

### Prerequisites

- .NET 8.0 SDK installed
- One of: SQL Server LocalDB, SQL Server, or Docker SQL Server

### Quick Setup Steps

#### Option A: Automated Setup (Recommended)

1. **Run the setup script:**

   ```powershell
   cd c:\Users\SivaSekharNalluri\Desktop\DirxNewSite
   .\setup.ps1
   ```

2. **Follow the wizard** to choose your database option

3. **Run the application:**

   ```powershell
   cd LeadManagementPortal
   dotnet run
   ```

4. **Open browser** and navigate to:

   - https://localhost:5001

5. **Login with default credentials:**
   - Email: `admin@leadportal.com`
   - Password: `Admin@123`

#### Option B: Manual Setup

1. **Navigate to project:**

   ```powershell
   cd c:\Users\SivaSekharNalluri\Desktop\DirxNewSite\LeadManagementPortal
   ```

2. **Restore packages:**

   ```powershell
   dotnet restore
   ```

3. **Update connection string** in `appsettings.json` (if needed)

4. **Create database:**

   ```powershell
   dotnet dotnet-ef migrations add InitialCreate
   dotnet dotnet-ef database update
   ```

5. **Run application:**

   ```powershell
   dotnet run
   ```

6. **Access at:** https://localhost:5001

### First Steps After Login

1. **Create a Sales Group**

   - Navigate to "Sales Groups"
   - Click "New Sales Group"
   - Enter group details

2. **Add Users**

   - Navigate to "Users"
   - Click "New User"
   - Assign roles:
     - **OrganizationAdmin**: Full access
     - **GroupAdmin**: Manage group leads
     - **SalesRep**: Own leads only

3. **Register Your First Lead**

   - Navigate to "Leads"
   - Click "New Lead"
   - Fill in lead information
   - Lead automatically gets 90-day expiry

4. **View Dashboard**
   - See overview of all leads
   - Check critical/high priority leads
   - Monitor conversion rates

### Key Features to Try

#### Lead Management

- Create leads with full contact info
- System prevents duplicates (same phone/email)
- Track lead status (New → Contacted → Qualified → Proposal → Negotiation → Converted)
- See days remaining before expiry

#### Lead Conversion

- Convert lead to customer from lead details page
- Customer record created automatically
- Original lead marked as converted

#### Lead Extension (Admin Only)

- Grant one-time 90-day extension to leads
- Track extension history

#### Dashboard Analytics

- Total leads count
- Pending vs converted metrics
- Critical and high-priority alerts
- Conversion rate tracking
- Average days to convert

### Role-Based Access

**Organization Admin Can:**

- View all leads and customers
- Create sales groups
- Add/manage users
- Grant lead extensions
- Change lead status
- Access all features

**Group Admin Can:**

- View leads in their group
- Manage group members
- Convert leads to customers
- Cannot modify restricted fields

**Sales Rep Can:**

- View only assigned leads
- Register new leads
- Update lead information
- Convert leads to customers
- Cannot change lead status

### Understanding Lead Lifecycle

```
New Lead (Day 0)
    ↓
Register → System assigns 90-day expiry
    ↓
Work on Lead (Days 1-90)
    ↓ (Option 1)
Convert → Becomes Customer ✓
    ↓ (Option 2)
Expire (Day 90+) → Available for re-registration
    ↓ (Option 3)
Extension (Admin only) → Additional 90 days (one-time)
```

### Urgency Indicators

- 🔴 **Critical** (Red): 7 days or less remaining
- 🟡 **High** (Amber): 8-30 days remaining
- 🔵 **Medium** (Blue): 31-60 days remaining
- ⚪ **Low** (Gray): 61+ days remaining

### Common Tasks

#### Register a New Lead

1. Click "New Lead" button
2. Fill required fields (marked with \*)
3. System validates for duplicates
4. Lead automatically assigned to you
5. 90-day countdown starts

#### Convert Lead to Customer

1. Open lead details
2. Click "Convert to Customer"
3. Confirm conversion
4. View customer in Customers section

#### Search for Leads

1. Use search box in Leads page
2. Filter by status
3. Results update automatically

#### Grant Extension (Admin)

1. Open lead details
2. Click "Grant Extension" (if available)
3. Confirm action
4. Lead gets additional 90 days

### Troubleshooting

**Can't login?**

- Use: admin@leadportal.com / Admin@123
- Check caps lock

**Database error?**

- Ensure SQL Server is running
- Check connection string
- See DATABASE_SETUP.md

**Can't create lead?**

- Phone/email may already exist
- Check for active leads with same contact

**Don't see all leads?**

- Check your role permissions
- SalesRep sees only assigned leads
- GroupAdmin sees only group leads

### Next Steps

1. **Customize Settings**

   - Update password policy in Program.cs
   - Modify lead expiry duration (default: 90 days)
   - Customize email templates (future feature)

2. **Add More Users**

   - Create sales teams
   - Assign group admins
   - Add sales reps

3. **Start Managing Leads**
   - Import existing leads (create manually for now)
   - Track conversions
   - Monitor performance

### Support & Documentation

- **Full Documentation**: See README.md
- **Database Setup**: See DATABASE_SETUP.md
- **Code Structure**: See project folders

### Default Test Data

After first run, you'll have:

- 1 Organization Admin user
- 3 predefined roles
- Empty sales groups (create your own)
- No leads (start registering)

---

**Ready to manage leads efficiently!** 🚀

For detailed feature explanations, see the main README.md file.
